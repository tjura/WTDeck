using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WTDeck.Core.Contracts;
using WTDeck.Ipc.Http;

namespace WTDeck.Ipc.Tests.Http;

public class HttpPluginBridgeTests : IAsyncLifetime
{
    private HttpPluginBridge _bridge = null!;
    private HttpClient _client = null!;
    private int _port;

    public async Task InitializeAsync()
    {
        _port = GetFreePort();
        var options = new HttpPluginBridgeOptions { Port = _port, BindAddress = "127.0.0.1" };
        _bridge = new HttpPluginBridge(options, NullLogger<HttpPluginBridge>.Instance);
        await _bridge.StartAsync(CancellationToken.None);
        _client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}") };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _bridge.StopAsync(CancellationToken.None);
        _bridge.Dispose();
    }

    [Fact]
    public async Task Get_state_returns_unknown_when_no_update_pushed()
    {
        var response = await _client.GetAsync("/api/stream-dock/state");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var snapshot = JsonSerializer.Deserialize<StreamDockStateSnapshot>(json, HttpJsonSerializer.Options);

        snapshot.Should().NotBeNull();
        snapshot!.State.GearStatus.Should().Be("unknown");
        snapshot.ProtocolVersion.Should().Be(IpcProtocol.Version);
    }

    [Fact]
    public async Task Get_state_returns_last_pushed_update()
    {
        var update = new ButtonStateUpdate(
            IpcProtocol.Version, "landing-gear", "GEAR DOWN", null,
            IsBlinking: true, IsEnabled: true, AlertLevel: "Info", StatusKey: "down");

        await _bridge.SendButtonStateAsync(update, CancellationToken.None);

        var response = await _client.GetAsync("/api/stream-dock/state");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var snapshot = JsonSerializer.Deserialize<StreamDockStateSnapshot>(json, HttpJsonSerializer.Options);

        snapshot.Should().NotBeNull();
        snapshot!.State.GearStatus.Should().Be("down");
        snapshot.State.GearTitle.Should().Be("GEAR DOWN");
        snapshot.State.GearBlinking.Should().BeTrue();
        snapshot.State.GearAlertLevel.Should().Be("Info");
    }

    [Fact]
    public async Task Post_action_raises_ButtonPressed_event()
    {
        ButtonPressCommand? captured = null;
        _bridge.ButtonPressed += (_, cmd) => captured = cmd;

        var response = await _client.PostAsync("/api/actions/landing-gear",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        captured.Should().NotBeNull();
        captured!.ActionKey.Should().Be("landing-gear");
    }

    [Fact]
    public async Task Put_status_accepts_heartbeat()
    {
        var body = """{"status":"connected"}""";
        var response = await _client.PutAsync("/api/stream-controller/status",
            new StringContent(body, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        var response = await _client.GetAsync("/api/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"status\":\"ok\"");
    }

    [Fact]
    public async Task Unknown_route_returns_404()
    {
        var response = await _client.GetAsync("/api/does-not-exist");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_status_with_invalid_json_returns_400()
    {
        var response = await _client.PutAsync("/api/stream-controller/status",
            new StringContent("{not valid", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
