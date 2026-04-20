using FluentAssertions;
using WTDeck.Core.Alerts;
using WTDeck.Core.Contracts;
using WTDeck.Core.Interfaces;
using WTDeck.Core.Models;
using WTDeck.Core.Profiles;
using WTDeck.Core.Profiles.Aircraft;
using WTDeck.Core.Rules;
using WTDeck.Core.Rules.Gear;

namespace WTDeck.App.IntegrationTests;

public class AppHostTests
{
    private static IRuleEngine CreateEngine()
    {
        var profiles = new AircraftProfileRegistry([A4NSkyhawkProfile.Instance]);
        var bindings = new AlertActionBindingRegistry();
        var alerts = new AlertCenter(bindings);
        var rules = new IRule[] { new GearButtonRule(), new GearOverspeedRule() };
        return new CompositeRuleEngine(rules, profiles, alerts, bindings, TimeProvider.System);
    }

    private static FlightSnapshot MakeSnapshot(float gearPercent, float iasKmh = 0f, bool valid = true, string? type = "a_4n")
        => new()
        {
            Valid = valid,
            Timestamp = DateTimeOffset.UtcNow,
            AircraftType = type,
            GearPercent = gearPercent,
            IndicatedAirspeedKmh = iasKmh,
        };

    [Fact]
    public void Telemetry_change_produces_correct_button_state()
    {
        var engine = CreateEngine();
        var snapshot = MakeSnapshot(gearPercent: 100f, iasKmh: 300f);

        var result = engine.Evaluate(snapshot, null);

        result.ButtonStates.Should().HaveCount(1);
        var button = result.ButtonStates[0];
        button.ActionKey.Should().Be("landing-gear");
        button.Title.Should().Be("GEAR DOWN");
        button.IsBlinking.Should().BeTrue();
    }

    [Fact]
    public void Button_state_maps_to_valid_ipc_message()
    {
        var engine = CreateEngine();
        // Damaged gear: stuck at 50% with lamp active across two ticks.
        var snapshot = new FlightSnapshot
        {
            Valid = true,
            Timestamp = DateTimeOffset.UtcNow,
            AircraftType = "a_4n",
            GearPercent = 50f,
            GearsLamp = 1.0f,
            IndicatedAirspeedKmh = 300f,
        };
        var prev = snapshot with { Timestamp = DateTimeOffset.UtcNow.AddMilliseconds(-100) };

        var result = engine.Evaluate(snapshot, prev);
        var button = result.ButtonStates[0];
        var update = new ButtonStateUpdate(
            IpcProtocol.Version,
            button.ActionKey,
            button.Title,
            null,
            button.IsBlinking,
            button.IsEnabled,
            button.AlertLevel.ToString());

        update.ProtocolVersion.Should().Be(IpcProtocol.Version);
        update.Title.Should().Be("DAMAGED");
        update.AlertLevel.Should().Be("Danger");
    }

    [Fact]
    public void Null_telemetry_produces_disabled_state()
    {
        var engine = CreateEngine();
        var result = engine.Evaluate(null, null);

        result.ButtonStates.Should().HaveCount(1);
        var button = result.ButtonStates[0];
        button.IsEnabled.Should().BeFalse();
        button.Title.Should().Be("NO GEAR");
    }

    [Fact]
    public void Gear_binding_resolves_to_key_chord()
    {
        var provider = WTDeck.Core.KeyBindings.BlkKeyBindingProvider.FromReader(new StringReader(""));
        var binding = provider.GetBinding(ActionId.Gear);

        binding.Should().NotBeNull();
        binding!.Chords.Should().HaveCount(1);
        binding.Chords[0].ScanCodes.Should().Equal(34);
    }

    [Fact]
    public void Null_keyboard_sender_records_chords()
    {
        var sender = new WTDeck.Input.Windows.NullKeyboardSender();
        var chord = new KeyChord([34]);

        sender.Send(chord);
        sender.Send(chord);

        sender.SentChords.Should().HaveCount(2);
        sender.SentChords[0].ScanCodes.Should().Equal(34);
    }

    [Fact]
    public void Gear_overspeed_end_to_end_full_scenario()
    {
        var profiles = new AircraftProfileRegistry([A4NSkyhawkProfile.Instance]);
        var bindings = new AlertActionBindingRegistry();
        var alerts = new AlertCenter(bindings);
        var rules = new IRule[] { new GearButtonRule(), new GearOverspeedRule() };
        var engine = new CompositeRuleEngine(rules, profiles, alerts, bindings, TimeProvider.System);

        // Step 1: level flight, gear up, ~300 km/h - no alert, gear-retracted.
        var step1 = engine.Evaluate(MakeSnapshot(gearPercent: 0f, iasKmh: 300f), previous: null);
        step1.ButtonStates[0].IconKey.Should().Be("gear-retracted");
        step1.AlertsSnapshot.Should().BeEmpty();

        // Step 2: gear extended at 500 km/h - overspeed alert should fire.
        var step2 = engine.Evaluate(
            MakeSnapshot(gearPercent: 100f, iasKmh: 500f),
            previous: MakeSnapshot(gearPercent: 100f, iasKmh: 300f));
        step2.ButtonStates[0].IconKey.Should().Be("gear-damaged");
        step2.ButtonStates[0].IsBlinking.Should().BeTrue();
        step2.ButtonStates[0].AlertLevel.Should().Be(AlertLevel.Danger);
        step2.AlertsSnapshot.Should().ContainSingle(a => a.Key == AlertKey.GearOverspeed && a.Status == AlertStatus.Active);

        // Step 3: pilot presses the button -> acknowledge.
        alerts.Acknowledge("landing-gear", DateTimeOffset.UtcNow).Should().BeTrue();
        var step3 = engine.Evaluate(
            MakeSnapshot(gearPercent: 100f, iasKmh: 500f),
            previous: MakeSnapshot(gearPercent: 100f, iasKmh: 500f));
        step3.ButtonStates[0].IconKey.Should().Be("gear-damaged");
        step3.ButtonStates[0].IsBlinking.Should().BeFalse();
        step3.ButtonStates[0].AlertLevel.Should().Be(AlertLevel.Danger);
        step3.AlertsSnapshot.Should().ContainSingle(a => a.Status == AlertStatus.Acknowledged);

        // Step 4: IAS drops below limit - alert clears, gear still down -> GEAR DOWN.
        var step4 = engine.Evaluate(
            MakeSnapshot(gearPercent: 100f, iasKmh: 400f),
            previous: MakeSnapshot(gearPercent: 100f, iasKmh: 500f));
        step4.AlertsSnapshot.Should().BeEmpty();
        step4.ButtonStates[0].IconKey.Should().Be("gear-deployed");

        // Step 5: IAS climbs back above limit -> FRESH Active alert (not Acknowledged).
        var step5 = engine.Evaluate(
            MakeSnapshot(gearPercent: 100f, iasKmh: 500f),
            previous: MakeSnapshot(gearPercent: 100f, iasKmh: 400f));
        step5.ButtonStates[0].IsBlinking.Should().BeTrue();
        step5.AlertsSnapshot.Should().ContainSingle(a => a.Status == AlertStatus.Active);
    }
}
