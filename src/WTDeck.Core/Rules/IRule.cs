using WTDeck.Core.Models;

namespace WTDeck.Core.Rules;

/// <summary>
/// A single flight-assistant rule. A rule reads the current context and
/// optionally emits zero or more button states and/or raises/clears alerts on
/// <see cref="RuleContext.AlertCenter"/>.
/// </summary>
public interface IRule
{
    IEnumerable<DeckButtonState> Apply(RuleContext context);
}
