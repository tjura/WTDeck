namespace WTDeck.Core.Contracts;

public sealed record StreamDockPanelUpdate(
    StreamDockPanelState Panel,
    IReadOnlyDictionary<string, StreamDockAlertState> Alerts)
{
    public static StreamDockPanelUpdate Unavailable() => new(
        StreamDockPanelState.Unavailable(),
        new Dictionary<string, StreamDockAlertState>(StringComparer.Ordinal)
        {
            [StreamDockAlertKeys.OverG] = StreamDockAlertState.Unavailable("G")
        });
}
