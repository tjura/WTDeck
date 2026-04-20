using WTDeck.Core.Models;
using WTDeck.Core.Rules;

namespace WTDeck.Core.Interfaces;

public interface IRuleEngine
{
    /// <summary>
    /// Evaluates all registered rules against the current snapshot and returns
    /// the resulting button states plus a snapshot of the alert center state.
    /// </summary>
    EvaluationResult Evaluate(FlightSnapshot? current, FlightSnapshot? previous);
}
