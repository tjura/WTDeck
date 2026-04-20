using FluentAssertions;
using WTDeck.Core.Models;
using WTDeck.Core.Rules;
using WTDeck.Core.Tests.TestDoubles;

namespace WTDeck.Core.Tests.Rules;

public class GearStateEvaluatorTests
{
    private static FlightSnapshot MakeState(float gearFraction, float lamp = 0f, bool valid = true)
        => new FlightSnapshotBuilder()
            .WithType("bf-109g-6")
            .WithGearPercent(gearFraction * 100f)
            .WithGearsLamp(lamp)
            .Build() with
        { Valid = valid };

    [Fact]
    public void Gears_zero_returns_Retracted()
    {
        GearStateEvaluator.Evaluate(MakeState(0.0f), null).Should().Be(GearState.Retracted);
    }

    [Fact]
    public void Gears_one_returns_Deployed()
    {
        GearStateEvaluator.Evaluate(MakeState(1.0f), null).Should().Be(GearState.Deployed);
    }

    [Fact]
    public void Gears_increasing_returns_Deploying()
    {
        var prev = MakeState(0.3f);
        var curr = MakeState(0.5f);
        GearStateEvaluator.Evaluate(curr, prev).Should().Be(GearState.Deploying);
    }

    [Fact]
    public void Gears_decreasing_returns_Retracting()
    {
        var prev = MakeState(0.7f);
        var curr = MakeState(0.5f);
        GearStateEvaluator.Evaluate(curr, prev).Should().Be(GearState.Retracting);
    }

    [Fact]
    public void Null_state_returns_Disabled()
    {
        GearStateEvaluator.Evaluate(null, null).Should().Be(GearState.Disabled);
    }

    [Fact]
    public void Invalid_state_returns_Disabled()
    {
        GearStateEvaluator.Evaluate(MakeState(0.5f, valid: false), null).Should().Be(GearState.Disabled);
    }

    [Fact]
    public void Threshold_boundary_deployed()
    {
        GearStateEvaluator.Evaluate(MakeState(0.96f), null).Should().Be(GearState.Deployed);
    }

    [Fact]
    public void Threshold_boundary_retracted()
    {
        GearStateEvaluator.Evaluate(MakeState(0.04f), null).Should().Be(GearState.Retracted);
    }

    [Fact]
    public void Damaged_when_lamp_active_and_stuck()
    {
        var prev = MakeState(0.5f, lamp: 1.0f);
        var curr = MakeState(0.5f, lamp: 1.0f);
        GearStateEvaluator.Evaluate(curr, prev).Should().Be(GearState.Damaged);
    }

    [Fact]
    public void Mid_position_without_previous_guesses_direction()
    {
        // gears > 0.5 without previous -> Deploying
        GearStateEvaluator.Evaluate(MakeState(0.7f), null).Should().Be(GearState.Deploying);
        // gears < 0.5 without previous -> Retracting
        GearStateEvaluator.Evaluate(MakeState(0.3f), null).Should().Be(GearState.Retracting);
    }
}
