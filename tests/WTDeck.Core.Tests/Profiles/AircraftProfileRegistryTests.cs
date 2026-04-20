using FluentAssertions;
using WTDeck.Core.Profiles;
using WTDeck.Core.Profiles.Aircraft;

namespace WTDeck.Core.Tests.Profiles;

public class AircraftProfileRegistryTests
{
    [Fact]
    public void Resolve_known_type_returns_specific_profile()
    {
        var registry = new AircraftProfileRegistry([A4NSkyhawkProfile.Instance]);
        var profile = registry.Resolve("a_4n");

        profile.Should().BeSameAs(A4NSkyhawkProfile.Instance);
        profile.GearOperatingSpeedKmh.Should().Be(450f);
    }

    [Fact]
    public void Resolve_unknown_type_returns_generic()
    {
        var registry = new AircraftProfileRegistry([A4NSkyhawkProfile.Instance]);
        registry.Resolve("unknown").Should().BeSameAs(AircraftProfile.Generic);
    }

    [Fact]
    public void Resolve_null_type_returns_generic()
    {
        var registry = new AircraftProfileRegistry([A4NSkyhawkProfile.Instance]);
        registry.Resolve(null).Should().BeSameAs(AircraftProfile.Generic);
    }

    [Fact]
    public void Case_insensitive_type_match()
    {
        var registry = new AircraftProfileRegistry([A4NSkyhawkProfile.Instance]);
        registry.Resolve("A_4N").Should().BeSameAs(A4NSkyhawkProfile.Instance);
    }

    [Fact]
    public void A4N_only_overrides_gear_operating_speed()
    {
        var a4n = A4NSkyhawkProfile.Instance;
        a4n.GearOperatingSpeedKmh.Should().Be(450f);
        // All other limits should match Generic until real values are added
        a4n.FlapsOperatingSpeedKmh.Should().Be(AircraftProfile.Generic.FlapsOperatingSpeedKmh);
        a4n.VneIasKmh.Should().Be(AircraftProfile.Generic.VneIasKmh);
    }
}
