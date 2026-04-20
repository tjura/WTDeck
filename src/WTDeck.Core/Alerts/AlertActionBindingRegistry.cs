namespace WTDeck.Core.Alerts;

public sealed class AlertActionBindingRegistry : IAlertActionBindingRegistry
{
    private static readonly Dictionary<string, HashSet<string>> ByAction = new(StringComparer.OrdinalIgnoreCase)
    {
        ["landing-gear"] = new(StringComparer.Ordinal) { AlertKey.GearOverspeed },
        // Future bindings:
        // ["flaps"]     = new() { AlertKey.FlapsOverspeed },
        // ["airbrake"]  = new() { AlertKey.Vne },
    };

    private static readonly Dictionary<string, string> ByAlert =
        ByAction
            .SelectMany(kv => kv.Value.Select(alertKey => (alertKey, actionKey: kv.Key)))
            .ToDictionary(x => x.alertKey, x => x.actionKey, StringComparer.Ordinal);

    public IReadOnlyCollection<string> AlertKeysForAction(string actionKey)
        => ByAction.TryGetValue(actionKey, out var set) ? set : Array.Empty<string>();

    public string? ActionKeyForAlert(string alertKey)
        => ByAlert.GetValueOrDefault(alertKey);
}
