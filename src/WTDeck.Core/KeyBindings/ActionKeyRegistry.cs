using WTDeck.Core.Models;

namespace WTDeck.Core.KeyBindings;

/// <summary>
/// Maps IPC string ActionKeys (sent from the plugin, e.g. "landing-gear") to
/// the typed <see cref="ActionId"/> enum used by <see cref="KeyBinding"/>
/// lookup. Centralised here so new buttons don't need to touch AppHost's
/// switch logic.
/// </summary>
public static class ActionKeyRegistry
{
    private static readonly Dictionary<string, ActionId> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["landing-gear"] = ActionId.Gear,
        ["flaps"] = ActionId.Flaps,
        ["airbrake"] = ActionId.AirBrake,
        ["bombs"] = ActionId.Bombs,
    };

    public static bool TryGetActionId(string actionKey, out ActionId actionId)
        => Map.TryGetValue(actionKey, out actionId);
}
