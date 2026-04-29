using WTDeck.Core.Models;

namespace WTDeck.Core.Rules.Flares;

public sealed class FlaresButtonRule : IRule
{
    private const string ActionKey = "launch-flares";

    public IEnumerable<DeckButtonState> Apply(RuleContext context)
    {
        if (context.Current is not { Valid: true } || !context.Profile.HasFlares)
        {
            yield return new DeckButtonState(ActionKey, "NO FLARES", "flare-unavailable", false, false, AlertLevel.None);
            yield break;
        }

        var count = context.Current.FlaresRemaining;
        if (count == 0)
        {
            yield return new DeckButtonState(ActionKey, "FLARES\n0", "flare-unavailable", false, false, AlertLevel.None);
            yield break;
        }

        if (count.HasValue)
        {
            yield return new DeckButtonState(ActionKey, $"FLARES\n{count.Value}", "flare-ready", false, true, AlertLevel.None);
            yield break;
        }

        yield return new DeckButtonState(ActionKey, "FLARES", "flare-unknown", false, true, AlertLevel.None);
    }
}
