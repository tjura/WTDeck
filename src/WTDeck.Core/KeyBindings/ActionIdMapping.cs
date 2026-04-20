using WTDeck.Core.Models;

namespace WTDeck.Core.KeyBindings;

public static class ActionIdMapping
{
    private static readonly Dictionary<string, ActionId> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ID_GEAR"] = ActionId.Gear,
        ["ID_FLAPS"] = ActionId.Flaps,
        ["ID_FLAPS_D"] = ActionId.Flaps,
        ["ID_FLAPS_U"] = ActionId.Flaps,
        ["ID_AIR_BRAKE"] = ActionId.AirBrake,
        ["ID_BOMBS"] = ActionId.Bombs,
    };

    public static ActionId FromBlkId(string blkId)
        => Map.GetValueOrDefault(blkId, ActionId.Unknown);
}
