using FluentAssertions;
using WTDeck.Core.Alerts;

namespace WTDeck.Core.Tests.Alerts;

public class AlertActionBindingRegistryTests
{
    private readonly AlertActionBindingRegistry _registry = new();

    [Fact]
    public void Landing_gear_action_is_bound_to_gear_overspeed_alert()
    {
        var alerts = _registry.AlertKeysForAction("landing-gear");
        alerts.Should().Contain(AlertKey.GearOverspeed);
    }

    [Fact]
    public void Gear_overspeed_alert_maps_back_to_landing_gear()
    {
        _registry.ActionKeyForAlert(AlertKey.GearOverspeed).Should().Be("landing-gear");
    }

    [Fact]
    public void Unknown_action_returns_empty()
    {
        _registry.AlertKeysForAction("unknown").Should().BeEmpty();
    }

    [Fact]
    public void Unknown_alert_returns_null()
    {
        _registry.ActionKeyForAlert("unknown").Should().BeNull();
    }
}
