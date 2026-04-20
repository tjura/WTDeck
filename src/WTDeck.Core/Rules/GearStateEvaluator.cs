using WTDeck.Core.Models;

namespace WTDeck.Core.Rules;

public static class GearStateEvaluator
{
    public static GearState Evaluate(FlightSnapshot? current, FlightSnapshot? previous)
    {
        if (current is null || !current.Valid)
            return GearState.Disabled;

        // Check for damaged state: gears_lamp anomaly.
        // When gear is stuck at a mid position with the lamp active, it indicates damage.
        if (current.GearsLamp > 0.5f && current.Gear > GearStateThresholds.RetractedThreshold
                                      && current.Gear < GearStateThresholds.DeployedThreshold
                                      && previous is not null
                                      && Math.Abs(current.Gear - previous.Gear) < 0.001f)
        {
            return GearState.Damaged;
        }

        if (current.Gear >= GearStateThresholds.DeployedThreshold)
            return GearState.Deployed;

        if (current.Gear <= GearStateThresholds.RetractedThreshold)
            return GearState.Retracted;

        // Transitioning - determine direction from previous state.
        if (previous is not null && previous.Valid)
        {
            if (current.Gear > previous.Gear)
                return GearState.Deploying;
            if (current.Gear < previous.Gear)
                return GearState.Retracting;
        }

        // No previous state to compare - guess from position.
        return current.Gear > 0.5f ? GearState.Deploying : GearState.Retracting;
    }
}
