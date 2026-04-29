using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WTDeck.App.Concurrency;
using WTDeck.Core.Alerts;
using WTDeck.Core.Contracts;
using WTDeck.Core.FlightAlerts;
using WTDeck.Core.Interfaces;
using WTDeck.Core.KeyBindings;
using WTDeck.Core.Mapping;
using WTDeck.Core.Models;
using WTDeck.Core.Profiles;
using WTDeck.Core.Rules;
using WTDeck.StreamDock.Interfaces;
using WTDeck.Telemetry;

namespace WTDeck.App;

public sealed class AppHost : BackgroundService
{
    private readonly TelemetryPollingService _telemetryPoller;
    private readonly IRuleEngine _ruleEngine;
    private readonly IPluginBridge _pluginBridge;
    private readonly IKeyBindingProvider _keyBindingProvider;
    private readonly IKeyboardSender _keyboardSender;
    private readonly IAlertCenter _alertCenter;
    private readonly IAircraftProfileRegistry _profiles;
    private readonly FlightAlertPanelEvaluator _flightAlertPanelEvaluator;
    private readonly IPluginSyncService _pluginSyncService;
    private readonly TimeProvider _clock;
    private readonly ILogger<AppHost> _logger;

    private FlightSnapshot? _previousSnapshot;
    private FlightSnapshot? _lastSnapshot;
    private readonly Dictionary<string, ButtonStateUpdate> _lastUpdateByAction = new(StringComparer.Ordinal);
    private StreamDockPanelUpdate? _lastPanelUpdate;
    private readonly SemaphoreSlim _evaluationLock = new(1, 1);
    private readonly LatestValueSignal<FlightSnapshot?> _telemetryUpdates = new();
    private Task? _telemetryProcessingTask;

    public AppHost(
        TelemetryPollingService telemetryPoller,
        IRuleEngine ruleEngine,
        IPluginBridge pluginBridge,
        IKeyBindingProvider keyBindingProvider,
        IKeyboardSender keyboardSender,
        IAlertCenter alertCenter,
        IAircraftProfileRegistry profiles,
        FlightAlertPanelEvaluator flightAlertPanelEvaluator,
        IPluginSyncService pluginSyncService,
        TimeProvider clock,
        ILogger<AppHost> logger)
    {
        _telemetryPoller = telemetryPoller;
        _ruleEngine = ruleEngine;
        _pluginBridge = pluginBridge;
        _keyBindingProvider = keyBindingProvider;
        _keyboardSender = keyboardSender;
        _alertCenter = alertCenter;
        _profiles = profiles;
        _flightAlertPanelEvaluator = flightAlertPanelEvaluator;
        _pluginSyncService = pluginSyncService;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _pluginBridge.ButtonPressed += OnButtonPressed;
        _telemetryPoller.StateChanged += OnTelemetryStateChanged;
        _telemetryProcessingTask = ProcessTelemetryUpdatesAsync(stoppingToken);

        try
        {
            // 1. Start the HTTP bridge first so the plugin can connect after StreamDock restarts.
            await _pluginBridge.StartAsync(stoppingToken);

            // 2. Sync plugin + profile to StreamDock, then restart Stream Controller.
            var syncResult = await _pluginSyncService.EnsureInstalledAsync(stoppingToken);
            if (syncResult.Warning is not null)
                _logger.LogWarning("StreamDock sync warning: {Warning}", syncResult.Warning);

            _logger.LogInformation("WTDeck host started");

            // 3. Run telemetry polling loop.
            await _telemetryPoller.RunAsync(stoppingToken);
        }
        finally
        {
            _telemetryPoller.StateChanged -= OnTelemetryStateChanged;
            _pluginBridge.ButtonPressed -= OnButtonPressed;
            _telemetryUpdates.Complete();
            if (_telemetryProcessingTask is not null)
            {
                try
                {
                    await _telemetryProcessingTask.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Telemetry processing loop did not complete within timeout");
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown.
                }
            }
            await _pluginBridge.StopAsync(CancellationToken.None);
            _logger.LogInformation("WTDeck host stopped");
        }
    }

    private void OnTelemetryStateChanged(object? sender, FlightSnapshot? snapshot)
    {
        _telemetryUpdates.TryPost(snapshot);
    }

    private async Task ProcessTelemetryUpdatesAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var snapshot in _telemetryUpdates.ReadAllAsync(ct))
            {
                try
                {
                    await RunRuleEngineAndPushAsync(snapshot, advanceHistory: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing telemetry state change");
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected during shutdown.
        }
    }

    private async Task RunRuleEngineAndPushAsync(FlightSnapshot? snapshot, bool advanceHistory)
    {
        await _evaluationLock.WaitAsync();
        try
        {
            var previous = advanceHistory ? _previousSnapshot : snapshot;
            var result = _ruleEngine.Evaluate(snapshot, previous);

            if (advanceHistory)
            {
                _previousSnapshot = snapshot;
                _lastSnapshot = snapshot;
            }

            var profile = _profiles.Resolve(snapshot?.AircraftType);
            var panelUpdate = _flightAlertPanelEvaluator.Evaluate(snapshot, profile);
            if (!PanelUpdatesEqual(_lastPanelUpdate, panelUpdate))
            {
                _lastPanelUpdate = panelUpdate;
                await _pluginBridge.SendPanelStateAsync(panelUpdate, CancellationToken.None);
                _logger.LogDebug(
                    "Sent panel state: {Status} (alerts={AlertCount})",
                    panelUpdate.Panel.StatusKey, panelUpdate.Alerts.Count);
            }

            foreach (var buttonState in result.ButtonStates)
            {
                var update = new ButtonStateUpdate(
                    IpcProtocol.Version,
                    buttonState.ActionKey,
                    buttonState.Title,
                    null,
                    buttonState.IsBlinking,
                    buttonState.IsEnabled,
                    buttonState.AlertLevel.ToString(),
                    DeckButtonStateMapper.ToStatusKey(buttonState.IconKey));

                if (_lastUpdateByAction.TryGetValue(update.ActionKey, out var last) && last == update)
                    continue;

                _lastUpdateByAction[update.ActionKey] = update;
                await _pluginBridge.SendButtonStateAsync(update, CancellationToken.None);
                _logger.LogDebug(
                    "Sent button state: {Action} {Title} (blink={Blink} alert={Alert})",
                    update.ActionKey, update.Title, update.IsBlinking, update.AlertLevel);
            }
        }
        finally
        {
            _evaluationLock.Release();
        }
    }

    private void OnButtonPressed(object? sender, ButtonPressCommand command)
    {
        _ = HandleButtonPressedAsync(command);
    }

    private async Task HandleButtonPressedAsync(ButtonPressCommand command)
    {
        try
        {
            _logger.LogInformation("Button pressed: {ActionKey}", command.ActionKey);

            if (string.Equals(command.ActionKey, StreamDockState.FlightAlertsActionKey, StringComparison.Ordinal))
            {
                _logger.LogDebug("Ignoring display-only action press: {ActionKey}", command.ActionKey);
                return;
            }

            // 1. Always try to acknowledge any active alerts for this action first.
            //    Running this before the key send means the visual/sound stop
            //    on the first press even if the game takes time to respond.
            var acknowledgedAny = _alertCenter.Acknowledge(command.ActionKey, _clock.GetUtcNow());
            if (acknowledgedAny)
                _logger.LogInformation("Acknowledged alerts for {ActionKey}", command.ActionKey);

            // 2. Resolve the typed ActionId and send the keyboard chord.
            if (ActionKeyRegistry.TryGetActionId(command.ActionKey, out var actionId))
            {
                var binding = _keyBindingProvider.GetBinding(actionId);
                if (binding is not null && binding.Chords.Count > 0)
                {
                    _keyboardSender.Send(binding.Chords[0]);
                    _logger.LogDebug("Sent chord for {Action}: [{ScanCodes}]",
                        command.ActionKey, string.Join(", ", binding.Chords[0].ScanCodes));
                }
            }
            else
            {
                _logger.LogDebug("No ActionId mapped for {ActionKey}", command.ActionKey);
            }

            // 3. Re-run the rule engine immediately so the acknowledged visual
            //    appears without waiting for the next telemetry tick.
            await RunRuleEngineAndPushAsync(_lastSnapshot, advanceHistory: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error handling button press: {ActionKey}", command.ActionKey);
        }
    }

    private static bool PanelUpdatesEqual(StreamDockPanelUpdate? left, StreamDockPanelUpdate right)
    {
        if (left is null)
            return false;

        if (left.Panel != right.Panel || left.Alerts.Count != right.Alerts.Count)
            return false;

        foreach (var (key, value) in left.Alerts)
        {
            if (!right.Alerts.TryGetValue(key, out var other) || value != other)
                return false;
        }

        return true;
    }
}
