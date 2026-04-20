using FluentAssertions;
using WTDeck.StreamDock.Profiles;

namespace WTDeck.StreamDock.Tests.Profiles;

public class DeterministicUuidTests
{
    [Fact]
    public void Same_input_produces_same_uuid()
    {
        var a = DeterministicUuid.Create("wtdeck", "landing-gear");
        var b = DeterministicUuid.Create("wtdeck", "landing-gear");
        a.Should().Be(b);
    }

    [Fact]
    public void Different_names_produce_different_uuids()
    {
        var a = DeterministicUuid.Create("wtdeck", "landing-gear");
        var b = DeterministicUuid.Create("wtdeck", "flaps");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Different_namespaces_produce_different_uuids()
    {
        var a = DeterministicUuid.Create("wtdeck", "landing-gear");
        var b = DeterministicUuid.Create("other", "landing-gear");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Generated_uuid_has_v5_version_nibble()
    {
        var uuid = DeterministicUuid.Create("wtdeck", "test");
        // Version 5 UUIDs have '5' at position 14 in the string representation:
        // xxxxxxxx-xxxx-5xxx-yxxx-xxxxxxxxxxxx
        var str = uuid.ToString("D");
        str[14].Should().Be('5');
    }
}
