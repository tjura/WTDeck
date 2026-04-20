using WTDeck.Core.Alerts;
using WTDeck.Core.Models;

namespace WTDeck.Core.Rules;

/// <summary>
/// Merges base rule outputs with alert overlays.
///
/// For every button state produced by a base rule, if the alert center has an
/// alert whose key is bound to the same action key, the composer overrides the
/// button's icon, blinking, and alert level so the warning is visible:
/// - <see cref="AlertStatus.Active"/>      -> icon=gear-damaged, blinking, Danger
/// - <see cref="AlertStatus.Acknowledged"/> -> icon=gear-damaged, NOT blinking, Danger
/// - <see cref="AlertStatus.Cleared"/>       -> no override (shouldn't appear)
/// </summary>
public static class DeckButtonStateComposer
{
    public static IReadOnlyList<DeckButtonState> Apply(
        IReadOnlyList<DeckButtonState> buttonStates,
        IReadOnlyCollection<Alert> activeAlerts,
        IAlertActionBindingRegistry bindings)
    {
        if (buttonStates.Count == 0 || activeAlerts.Count == 0)
            return buttonStates;

        var result = new List<DeckButtonState>(buttonStates.Count);
        foreach (var button in buttonStates)
        {
            var alertsForButton = bindings.AlertKeysForAction(button.ActionKey);
            if (alertsForButton.Count == 0)
            {
                result.Add(button);
                continue;
            }

            var overlayAlert = activeAlerts
                .Where(a => alertsForButton.Contains(a.Key) && a.Status != AlertStatus.Cleared)
                .OrderByDescending(a => a.Status == AlertStatus.Active)
                .ThenByDescending(a => a.Severity)
                .FirstOrDefault();

            if (overlayAlert is null)
            {
                result.Add(button);
                continue;
            }

            result.Add(button with
            {
                IconKey = OverlayIconFor(button.ActionKey),
                Title = overlayAlert.Message,
                IsBlinking = overlayAlert.Status == AlertStatus.Active,
                IsEnabled = true,
                AlertLevel = overlayAlert.Severity,
            });
        }

        return result;
    }

    private static string OverlayIconFor(string actionKey) => actionKey switch
    {
        "landing-gear" => "gear-damaged",
        // Future: "flaps" => "flaps-damaged", "airbrake" => "airbrake-damaged"
        _ => "gear-damaged",
    };
}
