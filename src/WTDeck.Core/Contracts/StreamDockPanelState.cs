namespace WTDeck.Core.Contracts;

public sealed record StreamDockPanelState(
    string StatusKey,
    bool IsAvailable)
{
    public const string StatusUnavailable = "unavailable";
    public const string StatusNormal = "normal";
    public const string StatusWarning = "warning";
    public const string StatusDanger = "danger";

    public static StreamDockPanelState Unavailable() => new(StatusUnavailable, false);
}
