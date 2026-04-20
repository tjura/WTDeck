using WTDeck.Core.Models;

namespace WTDeck.Core.Mapping;

/// <summary>
/// Maps domain <see cref="DeckButtonState.IconKey"/> values to
/// StreamDock plugin status keys (used by the HTTP API and plugin).
/// </summary>
public static class DeckButtonStateMapper
{
    public const string StatusRetracted = "up";
    public const string StatusDeployed = "down";
    public const string StatusDeploying = "extending";
    public const string StatusRetracting = "retracting";
    public const string StatusDamaged = "danger";
    public const string StatusDisabled = "unavailable";
    public const string StatusUnknown = "unknown";

    public static string ToStatusKey(string iconKey) => iconKey switch
    {
        "gear-retracted" => StatusRetracted,
        "gear-deployed" => StatusDeployed,
        "gear-deploying" => StatusDeploying,
        "gear-retracting" => StatusRetracting,
        "gear-damaged" => StatusDamaged,
        "gear-disabled" => StatusDisabled,
        _ => StatusUnknown,
    };
}
