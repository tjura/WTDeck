using FluentAssertions;
using WTDeck.Core.Mapping;

namespace WTDeck.Core.Tests.Mapping;

public class DeckButtonStateMapperTests
{
    [Theory]
    [InlineData("gear-retracted", "up")]
    [InlineData("gear-deployed", "down")]
    [InlineData("gear-deploying", "extending")]
    [InlineData("gear-retracting", "retracting")]
    [InlineData("gear-damaged", "danger")]
    [InlineData("gear-disabled", "unavailable")]
    public void Maps_known_icon_keys_to_status(string iconKey, string expectedStatus)
    {
        DeckButtonStateMapper.ToStatusKey(iconKey).Should().Be(expectedStatus);
    }

    [Fact]
    public void Unknown_icon_key_returns_unknown_status()
    {
        DeckButtonStateMapper.ToStatusKey("gear-unknown").Should().Be("unknown");
        DeckButtonStateMapper.ToStatusKey("some-random-thing").Should().Be("unknown");
    }
}
