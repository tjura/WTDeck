using System.Text.Json.Serialization;
using WTDeck.Core.Mapping;

namespace WTDeck.Core.Contracts;

public sealed record StreamDockState
{
    public const string LandingGearActionKey = "landing-gear";
    public const string LaunchFlaresActionKey = "launch-flares";
    public const string FlightAlertsActionKey = "flight-alerts";

    public StreamDockState(
        string gearStatus,
        string gearTitle,
        bool gearBlinking,
        string gearAlertLevel)
        : this(gearStatus, gearTitle, gearBlinking, gearAlertLevel, null, null, null)
    {
    }

    [JsonConstructor]
    public StreamDockState(
        string gearStatus,
        string gearTitle,
        bool gearBlinking,
        string gearAlertLevel,
        IReadOnlyDictionary<string, StreamDockActionState>? actions,
        IReadOnlyDictionary<string, StreamDockAlertState>? alerts,
        StreamDockPanelState? panel)
    {
        GearStatus = gearStatus;
        GearTitle = gearTitle;
        GearBlinking = gearBlinking;
        GearAlertLevel = gearAlertLevel;
        Actions = actions ?? BuildLegacyActions(gearStatus, gearTitle, gearBlinking, gearAlertLevel);
        Alerts = alerts ?? StreamDockPanelUpdate.Unavailable().Alerts;
        Panel = panel ?? StreamDockPanelUpdate.Unavailable().Panel;
    }

    public string GearStatus { get; init; }
    public string GearTitle { get; init; }
    public bool GearBlinking { get; init; }
    public string GearAlertLevel { get; init; }
    public IReadOnlyDictionary<string, StreamDockActionState> Actions { get; init; }
    public IReadOnlyDictionary<string, StreamDockAlertState> Alerts { get; init; }
    public StreamDockPanelState Panel { get; init; }

    public static StreamDockState FromActions(
        IReadOnlyDictionary<string, StreamDockActionState> actions,
        StreamDockPanelUpdate? panelUpdate = null)
    {
        var gear = actions.TryGetValue(LandingGearActionKey, out var gearState)
            ? gearState
            : UnknownActionState();

        panelUpdate ??= StreamDockPanelUpdate.Unavailable();

        return new StreamDockState(
            gear.StatusKey,
            gear.Title,
            gear.IsBlinking,
            gear.AlertLevel,
            actions,
            panelUpdate.Alerts,
            panelUpdate.Panel);
    }

    public static StreamDockActionState UnknownActionState() => new(
        DeckButtonStateMapper.StatusUnknown,
        "",
        false,
        false,
        "None");

    private static IReadOnlyDictionary<string, StreamDockActionState> BuildLegacyActions(
        string gearStatus,
        string gearTitle,
        bool gearBlinking,
        string gearAlertLevel)
        => new Dictionary<string, StreamDockActionState>(StringComparer.Ordinal)
        {
            [LandingGearActionKey] = new(gearStatus, gearTitle, gearBlinking, true, gearAlertLevel)
        };
}
