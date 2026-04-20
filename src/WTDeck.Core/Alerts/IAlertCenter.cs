using WTDeck.Core.Models;

namespace WTDeck.Core.Alerts;

/// <summary>
/// Stateful alert registry. Rules call <see cref="Raise"/> when a condition is
/// true and <see cref="Clear"/> when it is false. The AppHost calls
/// <see cref="Acknowledge"/> when the pilot presses a button to silence the
/// alarm without necessarily resolving the condition.
/// </summary>
public interface IAlertCenter
{
    /// <summary>
    /// Called each tick a condition is active.
    /// - If no alert exists with this key, creates a new one in <see cref="AlertStatus.Active"/>.
    /// - If an alert exists at the same severity, this is a no-op - preserves
    ///   the current status (including <see cref="AlertStatus.Acknowledged"/>).
    /// - Per the user-confirmed behaviour, this method does NOT re-raise an
    ///   acknowledged alert on severity escalation.
    /// </summary>
    Alert Raise(string key, AlertLevel severity, string message, DateTimeOffset now);

    /// <summary>
    /// Called each tick a condition is inactive. Removes the alert entirely if present.
    /// A subsequent <see cref="Raise"/> will create a fresh Active alert.
    /// </summary>
    void Clear(string key);

    /// <summary>
    /// Called by AppHost when the pilot presses a button.
    /// Demotes every Active alert whose key is bound to this action key to Acknowledged.
    /// Returns true if any alert was demoted.
    /// </summary>
    bool Acknowledge(string actionKey, DateTimeOffset now);

    /// <summary>Snapshot of all non-cleared alerts.</summary>
    IReadOnlyCollection<Alert> Current { get; }

    /// <summary>Find a specific alert by key.</summary>
    Alert? Find(string key);
}
