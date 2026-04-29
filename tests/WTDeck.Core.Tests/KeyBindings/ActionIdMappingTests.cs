using FluentAssertions;
using WTDeck.Core.KeyBindings;
using WTDeck.Core.Models;

namespace WTDeck.Core.Tests.KeyBindings;

public class ActionIdMappingTests
{
    [Theory]
    [InlineData("ID_GEAR", ActionId.Gear)]
    [InlineData("ID_COUNTERMEASURES_FLARES", ActionId.LaunchFlares)]
    [InlineData("ID_FLAPS", ActionId.Flaps)]
    [InlineData("ID_AIR_BRAKE", ActionId.AirBrake)]
    [InlineData("ID_BOMBS", ActionId.Bombs)]
    public void Maps_known_blk_ids(string blkId, ActionId expected)
    {
        ActionIdMapping.FromBlkId(blkId).Should().Be(expected);
    }

    [Fact]
    public void Unknown_id_returns_Unknown()
    {
        ActionIdMapping.FromBlkId("ID_DOES_NOT_EXIST").Should().Be(ActionId.Unknown);
    }

    [Fact]
    public void Mapping_is_case_insensitive()
    {
        ActionIdMapping.FromBlkId("id_gear").Should().Be(ActionId.Gear);
    }
}
