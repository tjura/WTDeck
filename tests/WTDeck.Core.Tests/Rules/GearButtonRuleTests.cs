using FluentAssertions;
using WTDeck.Core.Alerts;
using WTDeck.Core.Models;
using WTDeck.Core.Profiles;
using WTDeck.Core.Rules;
using WTDeck.Core.Rules.Gear;
using WTDeck.Core.Tests.TestDoubles;

namespace WTDeck.Core.Tests.Rules;

public class GearButtonRuleTests
{
    private readonly GearButtonRule _rule = new();

    private static RuleContext MakeContext(FlightSnapshot? current, FlightSnapshot? previous = null)
    {
        var bindings = new AlertActionBindingRegistry();
        var alerts = new AlertCenter(bindings);
        return new RuleContext(current, previous, AircraftProfile.Generic, alerts, DateTimeOffset.UtcNow);
    }

    private static FlightSnapshot MakeState(float gearFraction, float lamp = 0f, bool valid = true)
        => new FlightSnapshotBuilder()
            .WithGearPercent(gearFraction * 100f)
            .WithGearsLamp(lamp)
            .Build() with
        { Valid = valid };

    [Fact]
    public void Retracted_state_has_correct_properties()
    {
        var result = _rule.Apply(MakeContext(MakeState(0.0f))).Single();
        result.ActionKey.Should().Be("landing-gear");
        result.Title.Should().Be("GEAR UP");
        result.IsBlinking.Should().BeFalse();
        result.IsEnabled.Should().BeTrue();
        result.AlertLevel.Should().Be(AlertLevel.None);
    }

    [Fact]
    public void Deployed_state_blinks()
    {
        var result = _rule.Apply(MakeContext(MakeState(1.0f))).Single();
        result.Title.Should().Be("GEAR DOWN");
        result.IsBlinking.Should().BeTrue();
        result.AlertLevel.Should().Be(AlertLevel.Info);
    }

    [Fact]
    public void Deploying_state_blinks()
    {
        var result = _rule.Apply(MakeContext(MakeState(0.5f), MakeState(0.3f))).Single();
        result.Title.Should().Be("OPENING");
        result.IsBlinking.Should().BeTrue();
    }

    [Fact]
    public void Retracting_state_blinks()
    {
        var result = _rule.Apply(MakeContext(MakeState(0.5f), MakeState(0.7f))).Single();
        result.Title.Should().Be("CLOSING");
        result.IsBlinking.Should().BeTrue();
    }

    [Fact]
    public void Damaged_state_has_danger_alert()
    {
        var prev = MakeState(0.5f, lamp: 1.0f);
        var curr = MakeState(0.5f, lamp: 1.0f);
        var result = _rule.Apply(MakeContext(curr, prev)).Single();
        result.Title.Should().Be("DAMAGED");
        result.IsBlinking.Should().BeTrue();
        result.AlertLevel.Should().Be(AlertLevel.Danger);
    }

    [Fact]
    public void Disabled_state_is_not_enabled()
    {
        var result = _rule.Apply(MakeContext(null)).Single();
        result.Title.Should().Be("NO GEAR");
        result.IsEnabled.Should().BeFalse();
        result.IsBlinking.Should().BeFalse();
    }
}
