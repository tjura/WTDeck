using WTDeck.Core.Alerts;
using WTDeck.Core.Models;

namespace WTDeck.Core.Rules;

public sealed record EvaluationResult(
    IReadOnlyList<DeckButtonState> ButtonStates,
    IReadOnlyList<Alert> AlertsSnapshot);
