using System.Text.Json;
using FluentAssertions;
using WTDeck.Core.Contracts;

namespace WTDeck.Core.Tests.Contracts;

public class IpcMessageSerializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void ButtonStateUpdate_round_trips_through_json()
    {
        var original = new ButtonStateUpdate(1, "landing-gear", "GEAR DOWN", null, true, true, "Info");
        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<ButtonStateUpdate>(json, Options);
        deserialized.Should().Be(original);
    }

    [Fact]
    public void ButtonPressCommand_round_trips_through_json()
    {
        var original = new ButtonPressCommand(1, "landing-gear");
        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<ButtonPressCommand>(json, Options);
        deserialized.Should().Be(original);
    }

    [Fact]
    public void Deserialization_tolerates_unknown_fields()
    {
        var json = """{"protocolVersion":1,"actionKey":"landing-gear","extraField":"ignored"}""";
        var command = JsonSerializer.Deserialize<ButtonPressCommand>(json, Options);
        command.Should().NotBeNull();
        command!.ActionKey.Should().Be("landing-gear");
    }
}
