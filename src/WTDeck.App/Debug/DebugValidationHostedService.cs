using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WTDeck.App.Configuration;
using WTDeck.Core.Contracts;
using WTDeck.Core.Models;
using WTDeck.Input.Windows;
using WTDeck.Telemetry;

namespace WTDeck.App.Debug;

public sealed class DebugValidationHostedService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RuntimeModeOptions _runtimeMode;
    private readonly TelemetryPollingService _telemetryPoller;
    private readonly RecordingPluginBridge _pluginBridge;
    private readonly NullKeyboardSender _keyboardSender;
    private readonly DebugRunState _runState;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IServiceProvider _services;

    private string? _lastTelemetrySummary;
    private string? _lastUiSummary;

    public DebugValidationHostedService(
        RuntimeModeOptions runtimeMode,
        TelemetryPollingService telemetryPoller,
        RecordingPluginBridge pluginBridge,
        NullKeyboardSender keyboardSender,
        DebugRunState runState,
        IHostApplicationLifetime lifetime,
        IServiceProvider services)
    {
        _runtimeMode = runtimeMode;
        _telemetryPoller = telemetryPoller;
        _pluginBridge = pluginBridge;
        _keyboardSender = keyboardSender;
        _runState = runState;
        _lifetime = lifetime;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _pluginBridge.ButtonStateSent += OnButtonStateSent;

        try
        {
            if (_runtimeMode.EmulateApi)
            {
                var scenarioSource = _services.GetRequiredService<ScenarioTelemetrySource>();
                await RunScenarioAsync(scenarioSource, stoppingToken);
            }
            else
            {
                _telemetryPoller.StateChanged += OnLiveTelemetryStateChanged;
                WriteEvent(new
                {
                    @event = "debug_mode_started",
                    mode = "live",
                    message = "Debug mode is active. Side effects are disabled. Press Ctrl+C to stop."
                });

                try
                {
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                }
            }
        }
        finally
        {
            _telemetryPoller.StateChanged -= OnLiveTelemetryStateChanged;
            _pluginBridge.ButtonStateSent -= OnButtonStateSent;
        }
    }

    private async Task RunScenarioAsync(ScenarioTelemetrySource scenarioSource, CancellationToken ct)
    {
        WriteEvent(new
        {
            @event = "debug_mode_started",
            mode = "scenario",
            scenario = scenarioSource.Scenario.Name,
            stepIntervalMs = scenarioSource.Scenario.StepIntervalMs,
            message = "Scenario emulation is active. External side effects are disabled."
        });

        await foreach (var execution in scenarioSource.ReadExecutionsAsync(ct))
        {
            PrintTelemetry(execution.StepNumber, execution.Step.Name, execution.Snapshot, execution.IsAvailable);

            if (execution.Step.ExpectTelemetry is not null)
            {
                var telemetryError = ValidateTelemetry(execution.Step.ExpectTelemetry, execution.Snapshot, execution.IsAvailable);
                if (telemetryError is null)
                {
                    WriteEvent(new
                    {
                        @event = "gate_result",
                        gate = "telemetry",
                        step = execution.StepNumber,
                        name = execution.Step.Name,
                        passed = true
                    });
                }
                else
                {
                    _runState.FailTelemetry($"Step {execution.StepNumber}: {telemetryError}");
                    WriteEvent(new
                    {
                        @event = "gate_result",
                        gate = "telemetry",
                        step = execution.StepNumber,
                        name = execution.Step.Name,
                        passed = false,
                        error = telemetryError
                    });
                }
            }

            if (execution.Step.ExpectUi is not null)
                await ValidateUiAsync(execution.StepNumber, execution.Step.Name, execution.Step.ExpectUi, ct);

            if (execution.Step.Commands.Count > 0)
                await RunCommandsAsync(execution.StepNumber, execution.Step.Name, execution.Step.Commands, ct);
        }

        WriteEvent(new
        {
            @event = "summary",
            mode = "scenario",
            telemetryGatePassed = _runState.TelemetryGatePassed,
            uiGatePassed = _runState.UiGatePassed,
            failures = _runState.FailureCount
        });

        _lifetime.StopApplication();
    }

    private void OnLiveTelemetryStateChanged(object? sender, FlightSnapshot? snapshot)
    {
        var payload = new
        {
            valid = snapshot?.Valid ?? false,
            available = snapshot is not null,
            aircraftType = snapshot?.AircraftType,
            gearPercent = snapshot?.GearPercent ?? 0f,
            gear = snapshot?.Gear ?? 0f,
            gearsCommand = snapshot?.GearsCommand ?? 0f,
            gearsLamp = snapshot?.GearsLamp ?? 0f,
            indicatedAirspeedKmh = snapshot?.IndicatedAirspeedKmh ?? 0f
        };

        var summary = JsonSerializer.Serialize(payload, JsonOptions);
        if (summary == _lastTelemetrySummary)
            return;

        _lastTelemetrySummary = summary;
        WriteEvent(new
        {
            @event = "telemetry_state",
            mode = "live",
            step = (int?)null,
            name = (string?)null,
            payload
        });
    }

    private void OnButtonStateSent(object? sender, ButtonStateUpdate update)
    {
        var payload = new
        {
            actionKey = update.ActionKey,
            title = update.Title,
            statusKey = update.StatusKey,
            isBlinking = update.IsBlinking,
            isEnabled = update.IsEnabled,
            alertLevel = update.AlertLevel
        };

        var summary = JsonSerializer.Serialize(payload, JsonOptions);
        if (!_runtimeMode.EmulateApi && summary == _lastUiSummary)
            return;

        _lastUiSummary = summary;
        WriteEvent(new
        {
            @event = "ui_state",
            mode = _runtimeMode.EmulateApi ? "scenario" : "live",
            payload
        });
    }

    private void PrintTelemetry(int stepNumber, string? stepName, FlightSnapshot? snapshot, bool isAvailable)
    {
        WriteEvent(new
        {
            @event = "telemetry_state",
            mode = "scenario",
            step = stepNumber,
            name = stepName,
            payload = new
            {
                available = isAvailable,
                valid = snapshot?.Valid ?? false,
                aircraftType = snapshot?.AircraftType,
                gearPercent = snapshot?.GearPercent ?? 0f,
                gear = snapshot?.Gear ?? 0f,
                gearsCommand = snapshot?.GearsCommand ?? 0f,
                gearsLamp = snapshot?.GearsLamp ?? 0f,
                indicatedAirspeedKmh = snapshot?.IndicatedAirspeedKmh ?? 0f
            }
        });
    }

    private async Task ValidateUiAsync(int stepNumber, string? stepName, TelemetryScenarioUiExpectation expectation, CancellationToken ct)
    {
        var matched = await WaitForConditionAsync(() =>
        {
            if (!_pluginBridge.TryGetLatestState(expectation.ActionKey, out var update) || update is null)
                return false;

            return ValidateUi(expectation, update) is null;
        }, TimeSpan.FromSeconds(2), ct);

        if (matched && _pluginBridge.TryGetLatestState(expectation.ActionKey, out var update) && update is not null)
        {
            WriteEvent(new
            {
                @event = "gate_result",
                gate = "ui",
                step = stepNumber,
                name = stepName,
                passed = true,
                actionKey = expectation.ActionKey
            });
            return;
        }

        string error;
        if (_pluginBridge.TryGetLatestState(expectation.ActionKey, out var latest) && latest is not null)
            error = ValidateUi(expectation, latest) ?? "UI expectation did not match within timeout.";
        else
            error = $"No UI state was published for action '{expectation.ActionKey}'.";

        _runState.FailUi($"Step {stepNumber}: {error}");
        WriteEvent(new
        {
            @event = "gate_result",
            gate = "ui",
            step = stepNumber,
            name = stepName,
            passed = false,
            actionKey = expectation.ActionKey,
            error
        });
    }

    private async Task RunCommandsAsync(int stepNumber, string? stepName, IReadOnlyList<TelemetryScenarioCommand> commands, CancellationToken ct)
    {
        foreach (var command in commands)
        {
            var sentBefore = _keyboardSender.SentChords.Count;
            _pluginBridge.TriggerButtonPress(command.ActionKey);

            if (command.ExpectedScanCodes.Count > 0)
            {
                var keyMatched = await WaitForConditionAsync(() =>
                    _keyboardSender.SentChords.Count > sentBefore, TimeSpan.FromSeconds(2), ct);

                if (!keyMatched)
                {
                    var error = $"Command '{command.ActionKey}' did not produce a keyboard send.";
                    _runState.FailUi($"Step {stepNumber}: {error}");
                    WriteEvent(new
                    {
                        @event = "command_result",
                        step = stepNumber,
                        name = stepName,
                        actionKey = command.ActionKey,
                        passed = false,
                        error
                    });
                    continue;
                }

                var actual = _keyboardSender.SentChords[^1].ScanCodes;
                if (!actual.SequenceEqual(command.ExpectedScanCodes))
                {
                    var error = $"Command '{command.ActionKey}' expected scan codes [{string.Join(", ", command.ExpectedScanCodes)}] but got [{string.Join(", ", actual)}].";
                    _runState.FailUi($"Step {stepNumber}: {error}");
                    WriteEvent(new
                    {
                        @event = "command_result",
                        step = stepNumber,
                        name = stepName,
                        actionKey = command.ActionKey,
                        passed = false,
                        error
                    });
                    continue;
                }
            }

            if (command.ExpectedUi is not null)
                await ValidateUiAsync(stepNumber, stepName, command.ExpectedUi, ct);

            WriteEvent(new
            {
                @event = "command_result",
                step = stepNumber,
                name = stepName,
                actionKey = command.ActionKey,
                passed = true,
                scanCodes = command.ExpectedScanCodes.Count > 0 ? command.ExpectedScanCodes : null
            });
        }
    }

    private static string? ValidateTelemetry(TelemetryScenarioTelemetryExpectation expectation, FlightSnapshot? snapshot, bool isAvailable)
    {
        var mismatches = new List<string>();

        if (expectation.Available.HasValue && isAvailable != expectation.Available.Value)
            mismatches.Add($"expected available={expectation.Available.Value} but got {isAvailable}");

        if (expectation.Valid.HasValue && (snapshot?.Valid ?? false) != expectation.Valid.Value)
            mismatches.Add($"expected valid={expectation.Valid.Value} but got {snapshot?.Valid ?? false}");

        if (expectation.AircraftType is not null && snapshot?.AircraftType != expectation.AircraftType)
            mismatches.Add($"expected aircraftType='{expectation.AircraftType}' but got '{snapshot?.AircraftType ?? "<null>"}'");

        if (expectation.GearPercent.HasValue && !Approximately(snapshot?.GearPercent ?? 0f, expectation.GearPercent.Value))
            mismatches.Add($"expected gearPercent={expectation.GearPercent.Value} but got {snapshot?.GearPercent ?? 0f}");

        if (expectation.Gear.HasValue && !Approximately(snapshot?.Gear ?? 0f, expectation.Gear.Value))
            mismatches.Add($"expected gear={expectation.Gear.Value} but got {snapshot?.Gear ?? 0f}");

        if (expectation.GearsCommand.HasValue && !Approximately(snapshot?.GearsCommand ?? 0f, expectation.GearsCommand.Value))
            mismatches.Add($"expected gearsCommand={expectation.GearsCommand.Value} but got {snapshot?.GearsCommand ?? 0f}");

        if (expectation.GearsLamp.HasValue && !Approximately(snapshot?.GearsLamp ?? 0f, expectation.GearsLamp.Value))
            mismatches.Add($"expected gearsLamp={expectation.GearsLamp.Value} but got {snapshot?.GearsLamp ?? 0f}");

        if (expectation.IndicatedAirspeedKmh.HasValue && !Approximately(snapshot?.IndicatedAirspeedKmh ?? 0f, expectation.IndicatedAirspeedKmh.Value))
            mismatches.Add($"expected indicatedAirspeedKmh={expectation.IndicatedAirspeedKmh.Value} but got {snapshot?.IndicatedAirspeedKmh ?? 0f}");

        return mismatches.Count == 0 ? null : string.Join("; ", mismatches);
    }

    private static string? ValidateUi(TelemetryScenarioUiExpectation expectation, ButtonStateUpdate update)
    {
        var mismatches = new List<string>();

        if (!string.Equals(update.ActionKey, expectation.ActionKey, StringComparison.Ordinal))
            mismatches.Add($"expected actionKey='{expectation.ActionKey}' but got '{update.ActionKey}'");

        if (expectation.Title is not null && !string.Equals(update.Title, expectation.Title, StringComparison.Ordinal))
            mismatches.Add($"expected title='{expectation.Title}' but got '{update.Title}'");

        if (expectation.StatusKey is not null && !string.Equals(update.StatusKey, expectation.StatusKey, StringComparison.Ordinal))
            mismatches.Add($"expected statusKey='{expectation.StatusKey}' but got '{update.StatusKey}'");

        if (expectation.IsBlinking.HasValue && update.IsBlinking != expectation.IsBlinking.Value)
            mismatches.Add($"expected isBlinking={expectation.IsBlinking.Value} but got {update.IsBlinking}");

        if (expectation.IsEnabled.HasValue && update.IsEnabled != expectation.IsEnabled.Value)
            mismatches.Add($"expected isEnabled={expectation.IsEnabled.Value} but got {update.IsEnabled}");

        if (expectation.AlertLevel is not null && !string.Equals(update.AlertLevel, expectation.AlertLevel, StringComparison.Ordinal))
            mismatches.Add($"expected alertLevel='{expectation.AlertLevel}' but got '{update.AlertLevel}'");

        return mismatches.Count == 0 ? null : string.Join("; ", mismatches);
    }

    private static async Task<bool> WaitForConditionAsync(Func<bool> condition, TimeSpan timeout, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        while (DateTime.UtcNow - started < timeout)
        {
            if (condition())
                return true;

            await Task.Delay(20, ct);
        }

        return condition();
    }

    private static bool Approximately(float actual, float expected)
        => Math.Abs(actual - expected) <= 0.01f;

    private static void WriteEvent(object payload)
        => Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
}
