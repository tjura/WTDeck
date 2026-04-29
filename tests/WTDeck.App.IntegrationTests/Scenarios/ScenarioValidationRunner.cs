using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WTDeck.App.Debug;
using WTDeck.Core.Alerts;
using WTDeck.Core.Contracts;
using WTDeck.Core.FlightAlerts;
using WTDeck.Core.Interfaces;
using WTDeck.Core.KeyBindings;
using WTDeck.Core.Mapping;
using WTDeck.Core.Models;
using WTDeck.Core.Profiles;
using WTDeck.Core.Profiles.Aircraft;
using WTDeck.Core.Rules;
using WTDeck.Core.Rules.Flares;
using WTDeck.Core.Rules.Gear;
using WTDeck.Input.Windows;
using WTDeck.Telemetry;

namespace WTDeck.App.IntegrationTests.Scenarios;

internal static class ScenarioValidationRunner
{
    public static async Task ValidateAsync(string scenarioPath)
    {
        var scenario = TelemetryScenarioFile.LoadFromFile(scenarioPath);
        var source = new ScenarioTelemetrySource(scenario, NullLogger<ScenarioTelemetrySource>.Instance);
        var bridge = new RecordingPluginBridge();
        var keyboardSender = new NullKeyboardSender();
        var bindingProvider = BlkKeyBindingProvider.FromReader(new StringReader(""));

        var bindings = new AlertActionBindingRegistry();
        var alerts = new AlertCenter(bindings);
        var profiles = new AircraftProfileRegistry([A4NSkyhawkProfile.Instance]);
        var rules = new IRule[] { new GearButtonRule(), new FlaresButtonRule(), new GearOverspeedRule() };
        var engine = new CompositeRuleEngine(rules, profiles, alerts, bindings, TimeProvider.System);
        var panelEvaluator = new FlightAlertPanelEvaluator();

        FlightSnapshot? previous = null;

        for (var i = 0; i < scenario.Steps.Count; i++)
        {
            var step = scenario.Steps[i];
            var snapshot = await source.GetCurrentStateAsync(CancellationToken.None);
            var isAvailable = snapshot is not null;

            ValidateTelemetry(step, snapshot, isAvailable);

            var evaluation = engine.Evaluate(snapshot, previous);
            await PushUpdatesAsync(bridge, evaluation.ButtonStates);
            await PushPanelAsync(bridge, panelEvaluator, profiles, snapshot);
            ValidateUi(step, bridge);
            ValidatePanel(step, bridge);

            foreach (var command in step.Commands)
            {
                SimulateButtonPress(command, alerts, bindingProvider, keyboardSender);
                var commandEvaluation = engine.Evaluate(snapshot, snapshot);
                await PushUpdatesAsync(bridge, commandEvaluation.ButtonStates);
                await PushPanelAsync(bridge, panelEvaluator, profiles, snapshot);
                ValidateCommand(step, command, bridge, keyboardSender);
            }

            previous = snapshot;
        }
    }

    private static void ValidateTelemetry(TelemetryScenarioStep step, FlightSnapshot? snapshot, bool isAvailable)
    {
        var expectation = step.ExpectTelemetry;
        if (expectation is null)
            return;

        if (expectation.Available.HasValue)
            isAvailable.Should().Be(expectation.Available.Value, $"step '{step.Name}' available should match");

        if (expectation.Valid.HasValue)
            (snapshot?.Valid ?? false).Should().Be(expectation.Valid.Value, $"step '{step.Name}' valid should match");

        if (expectation.AircraftType is not null)
            snapshot?.AircraftType.Should().Be(expectation.AircraftType, $"step '{step.Name}' aircraft type should match");

        if (expectation.GearPercent.HasValue)
            (snapshot?.GearPercent ?? 0f).Should().BeApproximately(expectation.GearPercent.Value, 0.01f, $"step '{step.Name}' gear percent should match");

        if (expectation.Gear.HasValue)
            (snapshot?.Gear ?? 0f).Should().BeApproximately(expectation.Gear.Value, 0.01f, $"step '{step.Name}' gear fraction should match");

        if (expectation.GearsCommand.HasValue)
            (snapshot?.GearsCommand ?? 0f).Should().BeApproximately(expectation.GearsCommand.Value, 0.01f, $"step '{step.Name}' gear command should match");

        if (expectation.GearsLamp.HasValue)
            (snapshot?.GearsLamp ?? 0f).Should().BeApproximately(expectation.GearsLamp.Value, 0.01f, $"step '{step.Name}' gear lamp should match");

        if (expectation.IndicatedAirspeedKmh.HasValue)
            (snapshot?.IndicatedAirspeedKmh ?? 0f).Should().BeApproximately(expectation.IndicatedAirspeedKmh.Value, 0.01f, $"step '{step.Name}' IAS should match");

        if (expectation.LoadFactorNy.HasValue)
            (snapshot?.LoadFactorNy ?? 0f).Should().BeApproximately(expectation.LoadFactorNy.Value, 0.01f, $"step '{step.Name}' Ny should match");

        if (expectation.FlaresRemaining.HasValue)
            snapshot?.FlaresRemaining.Should().Be(expectation.FlaresRemaining.Value, $"step '{step.Name}' flare count should match");
    }

    private static void ValidateUi(TelemetryScenarioStep step, RecordingPluginBridge bridge)
    {
        if (step.ExpectUi is null)
            return;

        bridge.TryGetLatestState(step.ExpectUi.ActionKey, out var update).Should().BeTrue($"step '{step.Name}' should publish UI state");
        update.Should().NotBeNull();
        ValidateUiExpectation(step.ExpectUi, update!, $"step '{step.Name}'");
    }

    private static void ValidatePanel(TelemetryScenarioStep step, RecordingPluginBridge bridge)
    {
        if (step.ExpectPanel is null)
            return;

        var update = bridge.LatestPanelState;
        ValidatePanelExpectation(step.ExpectPanel, update, $"step '{step.Name}'");
    }

    private static void ValidateCommand(
        TelemetryScenarioStep step,
        TelemetryScenarioCommand command,
        RecordingPluginBridge bridge,
        NullKeyboardSender keyboardSender)
    {
        if (command.ExpectedScanCodes.Count > 0)
        {
            keyboardSender.SentChords.Should().NotBeEmpty($"step '{step.Name}' command '{command.ActionKey}' should send a key chord");
            keyboardSender.SentChords[^1].ScanCodes.Should().Equal(command.ExpectedScanCodes, $"step '{step.Name}' command '{command.ActionKey}' should match scan codes");
        }

        if (command.ExpectedUi is not null)
        {
            bridge.TryGetLatestState(command.ExpectedUi.ActionKey, out var update).Should().BeTrue($"step '{step.Name}' command '{command.ActionKey}' should publish UI state");
            update.Should().NotBeNull();
            ValidateUiExpectation(command.ExpectedUi, update!, $"step '{step.Name}' command '{command.ActionKey}'");
        }
    }

    private static void ValidateUiExpectation(TelemetryScenarioUiExpectation expectation, ButtonStateUpdate update, string because)
    {
        update.ActionKey.Should().Be(expectation.ActionKey, because);

        if (expectation.Title is not null)
            update.Title.Should().Be(expectation.Title, because);

        if (expectation.StatusKey is not null)
            update.StatusKey.Should().Be(expectation.StatusKey, because);

        if (expectation.IsBlinking.HasValue)
            update.IsBlinking.Should().Be(expectation.IsBlinking.Value, because);

        if (expectation.IsEnabled.HasValue)
            update.IsEnabled.Should().Be(expectation.IsEnabled.Value, because);

        if (expectation.AlertLevel is not null)
            update.AlertLevel.Should().Be(expectation.AlertLevel, because);
    }

    private static void ValidatePanelExpectation(TelemetryScenarioPanelExpectation expectation, StreamDockPanelUpdate update, string because)
    {
        if (expectation.StatusKey is not null)
            update.Panel.StatusKey.Should().Be(expectation.StatusKey, because);

        if (expectation.IsAvailable.HasValue)
            update.Panel.IsAvailable.Should().Be(expectation.IsAvailable.Value, because);

        foreach (var (key, alertExpectation) in expectation.Alerts)
        {
            update.Alerts.Should().ContainKey(key, because);
            var alert = update.Alerts[key];

            if (alertExpectation.Label is not null)
                alert.Label.Should().Be(alertExpectation.Label, because);

            if (alertExpectation.Value is not null)
                alert.Value.Should().Be(alertExpectation.Value, because);

            if (alertExpectation.StatusKey is not null)
                alert.StatusKey.Should().Be(alertExpectation.StatusKey, because);

            if (alertExpectation.AlertLevel is not null)
                alert.AlertLevel.Should().Be(alertExpectation.AlertLevel, because);

            if (alertExpectation.IsAvailable.HasValue)
                alert.IsAvailable.Should().Be(alertExpectation.IsAvailable.Value, because);

            if (alertExpectation.NumericValue.HasValue)
                (alert.NumericValue ?? 0f).Should().BeApproximately(alertExpectation.NumericValue.Value, 0.01f, because);
        }
    }

    private static async Task PushUpdatesAsync(RecordingPluginBridge bridge, IReadOnlyList<DeckButtonState> buttonStates)
    {
        foreach (var buttonState in buttonStates)
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

            await bridge.SendButtonStateAsync(update, CancellationToken.None);
        }
    }

    private static Task PushPanelAsync(
        RecordingPluginBridge bridge,
        FlightAlertPanelEvaluator panelEvaluator,
        IAircraftProfileRegistry profiles,
        FlightSnapshot? snapshot)
    {
        var profile = profiles.Resolve(snapshot?.AircraftType);
        var update = panelEvaluator.Evaluate(snapshot, profile);
        return bridge.SendPanelStateAsync(update, CancellationToken.None);
    }

    private static void SimulateButtonPress(
        TelemetryScenarioCommand command,
        AlertCenter alerts,
        IKeyBindingProvider bindingProvider,
        NullKeyboardSender keyboardSender)
    {
        alerts.Acknowledge(command.ActionKey, DateTimeOffset.UtcNow);

        if (!ActionKeyRegistry.TryGetActionId(command.ActionKey, out var actionId))
            return;

        var binding = bindingProvider.GetBinding(actionId);
        if (binding is null || binding.Chords.Count == 0)
            return;

        keyboardSender.Send(binding.Chords[0]);
    }
}
