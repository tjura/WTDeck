using FluentAssertions;
using WTDeck.Core.KeyBindings;
using WTDeck.Core.Models;

namespace WTDeck.Core.Tests.KeyBindings;

public class ActionKeyRegistryTests
{
    [Theory]
    [InlineData("landing-gear", ActionId.Gear)]
    [InlineData("launch-flares", ActionId.LaunchFlares)]
    public void Maps_action_keys(string actionKey, ActionId expected)
    {
        ActionKeyRegistry.TryGetActionId(actionKey, out var actionId).Should().BeTrue();
        actionId.Should().Be(expected);
    }

    [Fact]
    public void Unknown_action_key_returns_false()
    {
        ActionKeyRegistry.TryGetActionId("does-not-exist", out _).Should().BeFalse();
    }
}
