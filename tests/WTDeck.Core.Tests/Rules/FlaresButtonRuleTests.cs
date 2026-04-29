using FluentAssertions;
using WTDeck.Core.Alerts;
using WTDeck.Core.Models;
using WTDeck.Core.Profiles;
using WTDeck.Core.Profiles.Aircraft;
using WTDeck.Core.Rules;
using WTDeck.Core.Rules.Flares;
using WTDeck.Core.Tests.TestDoubles;

namespace WTDeck.Core.Tests.Rules;

public class FlaresButtonRuleTests
{
    private readonly FlaresButtonRule _rule = new();

    private static RuleContext MakeContext(FlightSnapshot? current, AircraftProfile? profile = null)
    {
        var bindings = new AlertActionBindingRegistry();
        var alerts = new AlertCenter(bindings);
        return new RuleContext(current, null, profile ?? A4NSkyhawkProfile.Instance, alerts, DateTimeOffset.UtcNow);
    }

    private static FlightSnapshot MakeState(int? flaresRemaining = null, bool valid = true)
        => new FlightSnapshotBuilder()
            .WithType(A4NSkyhawkProfile.TypeKey)
            .WithFlaresRemaining(flaresRemaining)
            .Build() with
        { Valid = valid };

    [Fact]
    public void No_telemetry_disables_button()
    {
        var result = _rule.Apply(MakeContext(null)).Single();

        result.ActionKey.Should().Be("launch-flares");
        result.Title.Should().Be("NO FLARES");
        result.IconKey.Should().Be("flare-unavailable");
        result.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Non_flare_profile_disables_button()
    {
        var result = _rule.Apply(MakeContext(MakeState(60), AircraftProfile.Generic)).Single();

        result.Title.Should().Be("NO FLARES");
        result.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Known_positive_count_is_enabled_and_displayed()
    {
        var result = _rule.Apply(MakeContext(MakeState(42))).Single();

        result.Title.Should().Be("FLARES\n42");
        result.IconKey.Should().Be("flare-ready");
        result.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Unknown_count_is_enabled_for_flare_profile()
    {
        var result = _rule.Apply(MakeContext(MakeState())).Single();

        result.Title.Should().Be("FLARES");
        result.IconKey.Should().Be("flare-unknown");
        result.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Zero_count_disables_button()
    {
        var result = _rule.Apply(MakeContext(MakeState(0))).Single();

        result.Title.Should().Be("FLARES\n0");
        result.IconKey.Should().Be("flare-unavailable");
        result.IsEnabled.Should().BeFalse();
    }
}
