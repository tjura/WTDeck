using WTDeck.Core.Contracts;
using WTDeck.Core.Models;
using WTDeck.Core.Profiles;

namespace WTDeck.Core.FlightAlerts;

public sealed class FlightAlertPanelEvaluator
{
    public StreamDockPanelUpdate Evaluate(FlightSnapshot? snapshot, AircraftProfile profile)
    {
        if (snapshot is not { Valid: true })
            return StreamDockPanelUpdate.Unavailable();

        var positiveG = MathF.Max(0f, snapshot.LoadFactorNy);
        var status = StatusForPositiveG(positiveG, profile);
        var level = AlertLevelForStatus(status);

        var overG = new StreamDockAlertState(
            Label: "G",
            Value: positiveG.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
            StatusKey: status,
            AlertLevel: level.ToString(),
            IsAvailable: true,
            NumericValue: positiveG);

        return new StreamDockPanelUpdate(
            Panel: new StreamDockPanelState(status, true),
            Alerts: new Dictionary<string, StreamDockAlertState>(StringComparer.Ordinal)
            {
                [StreamDockAlertKeys.OverG] = overG
            });
    }

    private static string StatusForPositiveG(float positiveG, AircraftProfile profile)
    {
        if (positiveG >= profile.OverGDangerThreshold)
            return StreamDockAlertState.StatusDanger;

        if (positiveG >= profile.OverGWarningThreshold)
            return StreamDockAlertState.StatusWarning;

        return StreamDockAlertState.StatusNormal;
    }

    private static AlertLevel AlertLevelForStatus(string status) => status switch
    {
        StreamDockAlertState.StatusDanger => AlertLevel.Danger,
        StreamDockAlertState.StatusWarning => AlertLevel.Warning,
        _ => AlertLevel.None,
    };
}
