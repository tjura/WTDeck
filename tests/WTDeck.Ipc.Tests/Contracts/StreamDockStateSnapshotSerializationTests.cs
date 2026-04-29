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
            ProtocolVersion: IpcProtocol.Version,
            AppVersion: IpcProtocol.AppVersion,
            Timestamp: DateTimeOffset.UtcNow,
            State: state);

        var json = JsonSerializer.Serialize(original, HttpJsonSerializer.Options);
        json.Should().Contain("\"protocolVersion\":4");
        json.Should().Contain("\"gearStatus\":\"down\"");
        json.Should().Contain("\"actions\"");
        json.Should().Contain("\"alerts\"");
        json.Should().Contain("\"panel\"");

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
              "protocolVersion": 4,
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

    [Fact]
    public void Action_dictionary_round_trips()
    {
        var actions = new Dictionary<string, StreamDockActionState>
        {
            ["landing-gear"] = new("up", "GEAR UP", false, true, "None"),
            ["launch-flares"] = new("ready", "FLARES\n42", false, true, "None")
        };
        var state = StreamDockState.FromActions(actions);
        var original = new StreamDockStateSnapshot(
            ProtocolVersion: IpcProtocol.Version,
            AppVersion: IpcProtocol.AppVersion,
            Timestamp: DateTimeOffset.UtcNow,
            State: state);

        var json = JsonSerializer.Serialize(original, HttpJsonSerializer.Options);
        var deserialized = JsonSerializer.Deserialize<StreamDockStateSnapshot>(json, HttpJsonSerializer.Options);

        deserialized.Should().NotBeNull();
        deserialized!.State.Actions.Should().ContainKey("launch-flares");
        deserialized.State.Actions["launch-flares"].Title.Should().Be("FLARES\n42");
    }

    [Fact]
    public void Alert_panel_payload_round_trips()
    {
        var actions = new Dictionary<string, StreamDockActionState>
        {
            ["landing-gear"] = new("up", "GEAR UP", false, true, "None")
        };
        var panelUpdate = new StreamDockPanelUpdate(
            new StreamDockPanelState("warning", true),
            new Dictionary<string, StreamDockAlertState>
            {
                ["over-g"] = new("G", "10.0", "warning", "Warning", true, 10f)
            });
        var state = StreamDockState.FromActions(actions, panelUpdate);
        var original = new StreamDockStateSnapshot(
            ProtocolVersion: IpcProtocol.Version,
            AppVersion: IpcProtocol.AppVersion,
            Timestamp: DateTimeOffset.UtcNow,
            State: state);

        var json = JsonSerializer.Serialize(original, HttpJsonSerializer.Options);
        var deserialized = JsonSerializer.Deserialize<StreamDockStateSnapshot>(json, HttpJsonSerializer.Options);

        deserialized.Should().NotBeNull();
        deserialized!.State.Panel.StatusKey.Should().Be("warning");
        deserialized.State.Panel.IsAvailable.Should().BeTrue();
        deserialized.State.Alerts.Should().ContainKey("over-g");
        deserialized.State.Alerts["over-g"].Value.Should().Be("10.0");
        deserialized.State.Alerts["over-g"].NumericValue.Should().Be(10f);
    }
}
