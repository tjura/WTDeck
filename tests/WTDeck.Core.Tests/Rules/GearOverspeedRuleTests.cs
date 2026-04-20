using FluentAssertions;
using WTDeck.Core.Alerts;
using WTDeck.Core.Models;
using WTDeck.Core.Profiles;
using WTDeck.Core.Profiles.Aircraft;
using WTDeck.Core.Rules;
using WTDeck.Core.Rules.Gear;
using WTDeck.Core.Tests.TestDoubles;

namespace WTDeck.Core.Tests.Rules;

public class GearOverspeedRuleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 4, 5, 10, 0, 0, TimeSpan.Zero);
    private readonly GearOverspeedRule _rule = new();
    private readonly AlertCenter _alerts = new(new AlertActionBindingRegistry());

    private RuleContext MakeContext(FlightSnapshot? current, AircraftProfile? profile = null)
        => new(current, null, profile ?? A4NSkyhawkProfile.Instance, _alerts, T0);

    private static FlightSnapshot Fly(float gearPercent, float iasKmh, bool valid = true) =>
        new FlightSnapshotBuilder()
            .WithGearPercent(gearPercent)
            .WithIas(iasKmh)
            .Build() with
        { Valid = valid };

    [Fact]
    public void Gear_up_and_fast_no_alert()
    {
        _rule.Apply(MakeContext(Fly(gearPercent: 0f, iasKmh: 700f))).ToList();
        _alerts.Find(AlertKey.GearOverspeed).Should().BeNull();
    }

    [Fact]
    public void Gear_down_and_slow_no_alert()
    {
        _rule.Apply(MakeContext(Fly(gearPercent: 100f, iasKmh: 200f))).ToList();
        _alerts.Find(AlertKey.GearOverspeed).Should().BeNull();
    }

    [Fact]
    public void Gear_extended_and_over_limit_raises_alert()
    {
        _rule.Apply(MakeContext(Fly(gearPercent: 100f, iasKmh: 500f))).ToList();

        var alert = _alerts.Find(AlertKey.GearOverspeed);
        alert.Should().NotBeNull();
        alert!.Status.Should().Be(AlertStatus.Active);
        alert.Severity.Should().Be(AlertLevel.Danger);
        alert.Message.Should().Contain("450");
    }

    [Fact]
    public void Gear_at_exactly_5_percent_does_not_raise()
    {
        _rule.Apply(MakeContext(Fly(gearPercent: 5f, iasKmh: 500f))).ToList();
        _alerts.Find(AlertKey.GearOverspeed).Should().BeNull();
    }

    [Fact]
    public void Gear_at_6_percent_raises_when_fast()
    {
        _rule.Apply(MakeContext(Fly(gearPercent: 6f, iasKmh: 500f))).ToList();
        _alerts.Find(AlertKey.GearOverspeed)!.Status.Should().Be(AlertStatus.Active);
    }

    [Fact]
    public void Ias_at_exactly_limit_does_not_raise()
    {
        _rule.Apply(MakeContext(Fly(gearPercent: 100f, iasKmh: 450f))).ToList();
        _alerts.Find(AlertKey.GearOverspeed).Should().BeNull();
    }

    [Fact]
    public void Alert_clears_when_gear_fully_retracts()
    {
        _rule.Apply(MakeContext(Fly(gearPercent: 100f, iasKmh: 500f))).ToList();
        _alerts.Find(AlertKey.GearOverspeed).Should().NotBeNull();

        _rule.Apply(MakeContext(Fly(gearPercent: 0f, iasKmh: 500f))).ToList();
        _alerts.Find(AlertKey.GearOverspeed).Should().BeNull();
    }

    [Fact]
    public void Alert_clears_when_ias_drops_below_limit()
    {
        _rule.Apply(MakeContext(Fly(gearPercent: 100f, iasKmh: 500f))).ToList();
        _rule.Apply(MakeContext(Fly(gearPercent: 100f, iasKmh: 400f))).ToList();

        _alerts.Find(AlertKey.GearOverspeed).Should().BeNull();
    }

    [Fact]
    public void Acknowledged_alert_persists_while_condition_still_true()
    {
        _rule.Apply(MakeContext(Fly(gearPercent: 100f, iasKmh: 500f))).ToList();
        _alerts.Acknowledge("landing-gear", T0.AddSeconds(1));

        // Rule runs again on next tick, condition still true
        _rule.Apply(MakeContext(Fly(gearPercent: 100f, iasKmh: 500f))).ToList();

        var alert = _alerts.Find(AlertKey.GearOverspeed);
        alert.Should().NotBeNull();
        alert!.Status.Should().Be(AlertStatus.Acknowledged);
    }

    [Fact]
    public void Alert_re_raises_after_clear_then_retrigger()
    {
        // Trigger -> acknowledge -> clear (IAS drops) -> retrigger
        _rule.Apply(MakeContext(Fly(gearPercent: 100f, iasKmh: 500f))).ToList();
        _alerts.Acknowledge("landing-gear", T0.AddSeconds(1));
        _rule.Apply(MakeContext(Fly(gearPercent: 100f, iasKmh: 400f))).ToList();
        _alerts.Find(AlertKey.GearOverspeed).Should().BeNull();

        // Retrigger
        _rule.Apply(MakeContext(Fly(gearPercent: 100f, iasKmh: 500f))).ToList();

        var alert = _alerts.Find(AlertKey.GearOverspeed);
        alert!.Status.Should().Be(AlertStatus.Active);
    }

    [Fact]
    public void Uses_profile_limit_not_generic_default()
    {
        // A-4N has 450 km/h, Generic has 9999. Same IAS would raise on A-4N but not on Generic.
        _rule.Apply(MakeContext(Fly(gearPercent: 100f, iasKmh: 500f), AircraftProfile.Generic)).ToList();
        _alerts.Find(AlertKey.GearOverspeed).Should().BeNull();

        _rule.Apply(MakeContext(Fly(gearPercent: 100f, iasKmh: 500f), A4NSkyhawkProfile.Instance)).ToList();
        _alerts.Find(AlertKey.GearOverspeed).Should().NotBeNull();
    }

    [Fact]
    public void Invalid_snapshot_clears_alert()
    {
        _rule.Apply(MakeContext(Fly(gearPercent: 100f, iasKmh: 500f))).ToList();
        _alerts.Find(AlertKey.GearOverspeed).Should().NotBeNull();

        _rule.Apply(MakeContext(Fly(gearPercent: 100f, iasKmh: 500f, valid: false))).ToList();

        _alerts.Find(AlertKey.GearOverspeed).Should().BeNull();
    }

    [Fact]
    public void Null_snapshot_clears_alert()
    {
        _rule.Apply(MakeContext(Fly(gearPercent: 100f, iasKmh: 500f))).ToList();
        _alerts.Find(AlertKey.GearOverspeed).Should().NotBeNull();

        _rule.Apply(MakeContext(null)).ToList();

        _alerts.Find(AlertKey.GearOverspeed).Should().BeNull();
    }
}
