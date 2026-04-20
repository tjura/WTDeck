using WTDeck.Core.Alerts;
using WTDeck.Core.Interfaces;
using WTDeck.Core.Models;
using WTDeck.Core.Profiles;

namespace WTDeck.Core.Rules;

public sealed class CompositeRuleEngine : IRuleEngine
{
    private readonly IReadOnlyList<IRule> _rules;
    private readonly IAircraftProfileRegistry _profiles;
    private readonly IAlertCenter _alerts;
    private readonly IAlertActionBindingRegistry _bindings;
    private readonly TimeProvider _clock;

    public CompositeRuleEngine(
        IEnumerable<IRule> rules,
        IAircraftProfileRegistry profiles,
        IAlertCenter alerts,
        IAlertActionBindingRegistry bindings,
        TimeProvider clock)
    {
        _rules = rules.ToList().AsReadOnly();
        _profiles = profiles;
        _alerts = alerts;
        _bindings = bindings;
        _clock = clock;
    }

    public EvaluationResult Evaluate(FlightSnapshot? current, FlightSnapshot? previous)
    {
        var profile = _profiles.Resolve(current?.AircraftType);
        var ctx = new RuleContext(current, previous, profile, _alerts, _clock.GetUtcNow());

        var outputs = new List<DeckButtonState>();
        foreach (var rule in _rules)
        {
            foreach (var buttonState in rule.Apply(ctx))
                outputs.Add(buttonState);
        }

        var alerts = _alerts.Current;
        var composed = DeckButtonStateComposer.Apply(outputs, alerts, _bindings);
        return new EvaluationResult(composed, alerts.ToList().AsReadOnly());
    }
}
