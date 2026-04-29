namespace WTDeck.Core.Contracts;

public sealed record StreamDockAlertState(
    string Label,
    string Value,
    string StatusKey,
    string AlertLevel,
    bool IsAvailable,
    float? NumericValue = null)
{
    public const string StatusUnavailable = "unavailable";
    public const string StatusNormal = "normal";
    public const string StatusWarning = "warning";
    public const string StatusDanger = "danger";

    public static StreamDockAlertState Unavailable(string label) => new(
        label,
        "--",
        StatusUnavailable,
        "None",
        false,
        null);
}
