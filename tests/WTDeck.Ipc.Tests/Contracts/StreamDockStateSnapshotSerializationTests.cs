using System.Text.Json;
using FluentAssertions;
using WTDeck.Core.Contracts;
using WTDeck.Ipc.Http;

namespace WTDeck.Ipc.Tests.Contracts;

public class StreamDockStateSnapshotSerializationTests
{
    [Fact]
    public void Snapshot_round_trips_through_json_with_camelCase()
    {
        var state = new StreamDockState("down", "GEAR DOWN", true, "Info");
        var original = new StreamDockStateSnapshot(
            ProtocolVersion: 2,
            AppVersion: "0.1.0",
            Timestamp: DateTimeOffset.UtcNow,
            State: state);

        var json = JsonSerializer.Serialize(original, HttpJsonSerializer.Options);
        json.Should().Contain("\"protocolVersion\":2");
        json.Should().Contain("\"gearStatus\":\"down\"");

        var deserialized = JsonSerializer.Deserialize<StreamDockStateSnapshot>(json, HttpJsonSerializer.Options);
        deserialized.Should().NotBeNull();
        deserialized!.State.GearStatus.Should().Be("down");
        deserialized.State.GearTitle.Should().Be("GEAR DOWN");
    }

    [Fact]
    public void Deserialization_tolerates_unknown_fields()
    {
        var json = """
            {
              "protocolVersion": 2,
              "appVersion": "0.1.0",
              "timestamp": "2026-04-04T12:00:00Z",
              "state": {
                "gearStatus": "up",
                "gearTitle": "GEAR UP",
                "gearBlinking": false,
                "gearAlertLevel": "None",
                "extraUnknownField": "ignored"
              },
              "someFutureField": 42
            }
            """;

        var snapshot = JsonSerializer.Deserialize<StreamDockStateSnapshot>(json, HttpJsonSerializer.Options);
        snapshot.Should().NotBeNull();
        snapshot!.State.GearStatus.Should().Be("up");
    }
}
