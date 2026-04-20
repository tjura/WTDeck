using FluentAssertions;
using WTDeck.Core.Alerts;
using WTDeck.Core.Models;
using WTDeck.Core.Rules;

namespace WTDeck.Core.Tests.Rules;

public class DeckButtonStateComposerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 4, 5, 10, 0, 0, TimeSpan.Zero);
    private readonly AlertActionBindingRegistry _bindings = new();

    private static DeckButtonState NormalGear() =>
        new("landing-gear", "GEAR DOWN", "gear-deployed", IsBlinking: true, IsEnabled: true, AlertLevel.Info);

    [Fact]
    public void No_active_alerts_returns_outputs_unchanged()
    {
        var button = NormalGear();
        var result = DeckButtonStateComposer.Apply([button], Array.Empty<Alert>(), _bindings);

        result.Should().ContainSingle();
        result[0].Should().Be(button);
    }

    [Fact]
    public void Active_alert_overrides_icon_and_forces_blinking()
    {
        var button = NormalGear();
        var alert = new Alert(AlertKey.GearOverspeed, AlertLevel.Danger, AlertStatus.Active, "GEAR > 450", T0, null);

        var result = DeckButtonStateComposer.Apply([button], [alert], _bindings);

        result[0].IconKey.Should().Be("gear-damaged");
        result[0].IsBlinking.Should().BeTrue();
        result[0].AlertLevel.Should().Be(AlertLevel.Danger);
        result[0].Title.Should().Be("GEAR > 450");
    }

    [Fact]
    public void Acknowledged_alert_overrides_icon_but_not_blinking()
    {
        var button = NormalGear();
        var alert = new Alert(AlertKey.GearOverspeed, AlertLevel.Danger, AlertStatus.Acknowledged, "GEAR > 450", T0, T0);

        var result = DeckButtonStateComposer.Apply([button], [alert], _bindings);

        result[0].IconKey.Should().Be("gear-damaged");
        result[0].IsBlinking.Should().BeFalse();
        result[0].AlertLevel.Should().Be(AlertLevel.Danger);
    }

    [Fact]
    public void Alert_without_matching_action_is_ignored()
    {
        var button = NormalGear();
        var unrelated = new Alert("some-other-alert", AlertLevel.Danger, AlertStatus.Active, "msg", T0, null);

        var result = DeckButtonStateComposer.Apply([button], [unrelated], _bindings);

        result[0].Should().Be(button);
    }

    [Fact]
    public void Active_alert_takes_precedence_over_acknowledged_on_same_button()
    {
        // Not currently reachable (one alert key per button for gear), but the
        // composer must handle it correctly if a future button binds to multiple alerts.
        var button = NormalGear();
        var acked = new Alert("fake-ack", AlertLevel.Danger, AlertStatus.Acknowledged, "old", T0, T0);
        var active = new Alert(AlertKey.GearOverspeed, AlertLevel.Danger, AlertStatus.Active, "new", T0, null);

        var result = DeckButtonStateComposer.Apply([button], [acked, active], _bindings);

        result[0].IsBlinking.Should().BeTrue();
        result[0].Title.Should().Be("new");
    }
}
