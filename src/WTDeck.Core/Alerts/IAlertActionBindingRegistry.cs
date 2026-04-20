namespace WTDeck.Core.Alerts;

/// <summary>
/// Maps between button action keys (used by the plugin IPC, e.g. "landing-gear")
/// and the alert keys that a press on that button should acknowledge.
/// </summary>
public interface IAlertActionBindingRegistry
{
    /// <summary>Returns the set of alert keys a press on this action should acknowledge.</summary>
    IReadOnlyCollection<string> AlertKeysForAction(string actionKey);

    /// <summary>Reverse lookup: which action button is responsible for this alert key.</summary>
    string? ActionKeyForAlert(string alertKey);
}
