using WTDeck.Core.Models;

namespace WTDeck.Core.Rules.Gear;

/// <summary>
/// Base rule for the landing-gear button. Produces the nominal
/// <see cref="DeckButtonState"/> based on the current gear position and
/// transition direction. Alert overlays (e.g. gear-overspeed) are applied
/// separately by <see cref="DeckButtonStateComposer"/>.
/// </summary>
public sealed class GearButtonRule : IRule
{
    private const string ActionKey = "landing-gear";

    public IEnumerable<DeckButtonState> Apply(RuleContext context)
    {
        var gearState = GearStateEvaluator.Evaluate(context.Current, context.Previous);

        yield return gearState switch
        {
            GearState.Retracted => new DeckButtonState(ActionKey, "GEAR UP", "gear-retracted", false, true, AlertLevel.None),
            GearState.Deployed => new DeckButtonState(ActionKey, "GEAR DOWN", "gear-deployed", true, true, AlertLevel.Info),
            GearState.Deploying => new DeckButtonState(ActionKey, "OPENING", "gear-deploying", true, true, AlertLevel.Info),
            GearState.Retracting => new DeckButtonState(ActionKey, "CLOSING", "gear-retracting", true, true, AlertLevel.Info),
            GearState.Damaged => new DeckButtonState(ActionKey, "DAMAGED", "gear-damaged", true, true, AlertLevel.Danger),
            GearState.Disabled => new DeckButtonState(ActionKey, "NO GEAR", "gear-disabled", false, false, AlertLevel.None),
            _ => new DeckButtonState(ActionKey, "---", "gear-unknown", false, false, AlertLevel.None),
        };
    }
}
