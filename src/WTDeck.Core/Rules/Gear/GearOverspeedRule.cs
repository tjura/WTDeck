using WTDeck.Core.Alerts;
using WTDeck.Core.Models;

namespace WTDeck.Core.Rules.Gear;

/// <summary>
/// Raises a <see cref="AlertKey.GearOverspeed"/> alert when the landing gear is
/// not fully retracted and indicated airspeed exceeds the aircraft's gear
/// operating limit (Vlo). Clears the alert when either condition no longer
/// holds. Emits no button states of its own - the composer overlays the alert
/// onto the landing-gear button.
/// </summary>
public sealed class GearOverspeedRule : IRule
{
    /// <summary>Threshold on /state "gear, %" above which gear is considered "exposed".</summary>
    private const float GearExposedMinPercent = 5f;

    public IEnumerable<DeckButtonState> Apply(RuleContext context)
    {
        var snapshot = context.Current;
        if (snapshot is null || !snapshot.Valid)
        {
            context.AlertCenter.Clear(AlertKey.GearOverspeed);
            yield break;
        }

        var gearExposed = snapshot.GearPercent > GearExposedMinPercent;
        var overLimit = snapshot.IndicatedAirspeedKmh > context.Profile.GearOperatingSpeedKmh;

        if (gearExposed && overLimit)
        {
            var message = $"GEAR > {context.Profile.GearOperatingSpeedKmh:0} KM/H";
            context.AlertCenter.Raise(AlertKey.GearOverspeed, AlertLevel.Danger, message, context.Now);
        }
        else
        {
            context.AlertCenter.Clear(AlertKey.GearOverspeed);
        }

        yield break;
    }
}
