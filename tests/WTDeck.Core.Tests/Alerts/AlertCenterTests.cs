using FluentAssertions;
using WTDeck.Core.Alerts;
using WTDeck.Core.Models;

namespace WTDeck.Core.Tests.Alerts;

public class AlertCenterTests
{
    private static readonly DateTimeOffset T0 = new(2026, 4, 5, 10, 0, 0, TimeSpan.Zero);

    private static AlertCenter NewCenter() =>
        new(new AlertActionBindingRegistry());

    [Fact]
    public void Raise_when_not_existing_creates_Active_alert()
    {
        var center = NewCenter();

        var alert = center.Raise(AlertKey.GearOverspeed, AlertLevel.Danger, "GEAR > 450", T0);

        alert.Status.Should().Be(AlertStatus.Active);
        alert.Severity.Should().Be(AlertLevel.Danger);
        alert.Message.Should().Be("GEAR > 450");
        alert.RaisedAt.Should().Be(T0);
        alert.AcknowledgedAt.Should().BeNull();
    }

    [Fact]
    public void Raise_same_severity_is_idempotent()
    {
        var center = NewCenter();

        var first = center.Raise(AlertKey.GearOverspeed, AlertLevel.Danger, "GEAR > 450", T0);
        var second = center.Raise(AlertKey.GearOverspeed, AlertLevel.Danger, "GEAR > 450", T0.AddSeconds(1));

        // Second raise should NOT update the timestamp (idempotent semantic per decision)
        second.RaisedAt.Should().Be(T0);
        first.Should().Be(second);
    }

    [Fact]
    public void Clear_removes_alert()
    {
        var center = NewCenter();
        center.Raise(AlertKey.GearOverspeed, AlertLevel.Danger, "msg", T0);

        center.Clear(AlertKey.GearOverspeed);

        center.Find(AlertKey.GearOverspeed).Should().BeNull();
        center.Current.Should().BeEmpty();
    }

    [Fact]
    public void Clear_then_Raise_creates_fresh_Active_not_Acknowledged()
    {
        var center = NewCenter();
        center.Raise(AlertKey.GearOverspeed, AlertLevel.Danger, "msg", T0);
        center.Acknowledge("landing-gear", T0.AddSeconds(1)).Should().BeTrue();
        center.Find(AlertKey.GearOverspeed)!.Status.Should().Be(AlertStatus.Acknowledged);

        center.Clear(AlertKey.GearOverspeed);
        var fresh = center.Raise(AlertKey.GearOverspeed, AlertLevel.Danger, "msg", T0.AddSeconds(2));

        fresh.Status.Should().Be(AlertStatus.Active);
    }

    [Fact]
    public void Acknowledge_demotes_active_alerts_bound_to_that_action()
    {
        var center = NewCenter();
        center.Raise(AlertKey.GearOverspeed, AlertLevel.Danger, "msg", T0);

        var result = center.Acknowledge("landing-gear", T0.AddSeconds(5));

        result.Should().BeTrue();
        var alert = center.Find(AlertKey.GearOverspeed)!;
        alert.Status.Should().Be(AlertStatus.Acknowledged);
        alert.AcknowledgedAt.Should().Be(T0.AddSeconds(5));
    }

    [Fact]
    public void Acknowledge_does_not_affect_unrelated_alerts()
    {
        var center = NewCenter();
        center.Raise(AlertKey.GearOverspeed, AlertLevel.Danger, "msg", T0);

        // "flaps" is not bound to GearOverspeed
        var result = center.Acknowledge("flaps", T0);

        result.Should().BeFalse();
        center.Find(AlertKey.GearOverspeed)!.Status.Should().Be(AlertStatus.Active);
    }

    [Fact]
    public void Acknowledge_with_no_active_alerts_returns_false()
    {
        var center = NewCenter();

        var result = center.Acknowledge("landing-gear", T0);

        result.Should().BeFalse();
    }

    [Fact]
    public void Acknowledged_alert_stays_acknowledged_on_re_raise()
    {
        // User-confirmed behaviour: a re-raise while acknowledged does NOT
        // flip the alert back to Active, even on severity escalation.
        var center = NewCenter();
        center.Raise(AlertKey.GearOverspeed, AlertLevel.Danger, "msg", T0);
        center.Acknowledge("landing-gear", T0.AddSeconds(1));

        center.Raise(AlertKey.GearOverspeed, AlertLevel.Danger, "msg", T0.AddSeconds(2));

        center.Find(AlertKey.GearOverspeed)!.Status.Should().Be(AlertStatus.Acknowledged);
    }

    [Fact]
    public void Acknowledged_alert_stays_in_Current_until_Clear()
    {
        var center = NewCenter();
        center.Raise(AlertKey.GearOverspeed, AlertLevel.Danger, "msg", T0);
        center.Acknowledge("landing-gear", T0.AddSeconds(1));

        center.Current.Should().HaveCount(1);
        center.Current.First().Status.Should().Be(AlertStatus.Acknowledged);
    }
}
