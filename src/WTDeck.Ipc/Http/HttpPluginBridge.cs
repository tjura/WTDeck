using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WTDeck.Core.Contracts;
using WTDeck.Core.Interfaces;
using WTDeck.Core.Mapping;

namespace WTDeck.Ipc.Http;

/// <summary>
/// HTTP-based implementation of <see cref="IPluginBridge"/>.
/// Exposes a minimal REST API on a loopback port that the StreamDock plugin polls.
/// </summary>
public sealed class HttpPluginBridge : IPluginBridge, IDisposable
{
    private static readonly string[] PublishedActionKeys =
    [
        StreamDockState.LandingGearActionKey,
        StreamDockState.LaunchFlaresActionKey
    ];

    private readonly HttpPluginBridgeOptions _options;
    private readonly ILogger<HttpPluginBridge> _logger;
    private readonly HttpListener _listener = new();
    private readonly HttpRouter _router = new();
    private readonly ConcurrentDictionary<string, ButtonStateUpdate> _stateByActionKey = new();
    private StreamDockPanelUpdate _lastPanelUpdate = StreamDockPanelUpdate.Unavailable();
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private DateTimeOffset _lastHeartbeat = DateTimeOffset.MinValue;
    private string _lastClientStatus = "unknown";

    public event EventHandler<ButtonPressCommand>? ButtonPressed;

    public HttpPluginBridge(HttpPluginBridgeOptions options, ILogger<HttpPluginBridge> logger)
    {
        _options = options;
        _logger = logger;

        var prefix = $"http://{_options.BindAddress}:{_options.Port}/";
        _listener.Prefixes.Add(prefix);

        RegisterRoutes();
    }

    public Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _listener.Start();
        _logger.LogInformation("HTTP plugin bridge listening on {Prefix}", _listener.Prefixes.First());
        _listenerTask = Task.Run(() => AcceptLoopAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _cts?.Cancel();
        try
        {
            if (_listener.IsListening)
                _listener.Stop();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        if (_listenerTask is not null)
        {
            try
            {
                await _listenerTask.WaitAsync(TimeSpan.FromSeconds(2), ct);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Listener task did not complete within timeout");
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }

        _logger.LogInformation("HTTP plugin bridge stopped");
    }

    public Task SendButtonStateAsync(ButtonStateUpdate update, CancellationToken ct)
    {
        _stateByActionKey[update.ActionKey] = update;
        return Task.CompletedTask;
    }

    public Task SendPanelStateAsync(StreamDockPanelUpdate update, CancellationToken ct)
    {
        _lastPanelUpdate = update;
        return Task.CompletedTask;
    }

    public ButtonStateUpdate? GetButtonState(string actionKey) =>
        _stateByActionKey.TryGetValue(actionKey, out var state) ? state : null;

    private void RegisterRoutes()
    {
        _router.Map("GET", "/api/stream-dock/state", HandleGetState);
        _router.Map("POST", "/api/actions/{actionKey}", HandlePostAction);
        _router.Map("PUT", "/api/stream-controller/status", HandlePutStatus);
        _router.Map("GET", "/api/health", HandleHealth);
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleRequestAsync(context, ct), ct);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
    {
        try
        {
            var handled = await _router.DispatchAsync(context, ct);
            if (!handled)
            {
                await WriteResponseAsync(context, 404, new { error = "not_found" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HTTP request handler failed for {Method} {Path}",
                context.Request.HttpMethod, context.Request.Url?.AbsolutePath);
            try
            {
                await WriteResponseAsync(context, 500, new { error = "internal_error" });
            }
            catch
            {
                // Response may already be closed
            }
        }
        finally
        {
            try { context.Response.Close(); } catch { /* ignore */ }
        }
    }

    private async Task HandleGetState(HttpListenerContext context, RouteMatch match, CancellationToken ct)
    {
        var actions = _stateByActionKey.ToDictionary(
            pair => pair.Key,
            pair => ToActionState(pair.Value),
            StringComparer.Ordinal);

        foreach (var actionKey in PublishedActionKeys)
        {
            actions.TryAdd(actionKey, StreamDockState.UnknownActionState());
        }

        var state = StreamDockState.FromActions(actions, _lastPanelUpdate);

        var snapshot = new StreamDockStateSnapshot(
            ProtocolVersion: IpcProtocol.Version,
            AppVersion: IpcProtocol.AppVersion,
            Timestamp: DateTimeOffset.UtcNow,
            State: state);

        await WriteResponseAsync(context, 200, snapshot);
    }

    private static StreamDockActionState ToActionState(ButtonStateUpdate update) => new(
        update.StatusKey ?? DeckButtonStateMapper.StatusUnknown,
        update.Title,
        update.IsBlinking,
        update.IsEnabled,
        update.AlertLevel);

    private async Task HandlePostAction(HttpListenerContext context, RouteMatch match, CancellationToken ct)
    {
        var actionKey = match.GetString("actionKey");
        if (string.IsNullOrEmpty(actionKey))
        {
            await WriteResponseAsync(context, 400, new ActionTriggerResponse(false, "missing_action_key"));
            return;
        }

        _logger.LogDebug("Button press received for {ActionKey}", actionKey);

        var command = new ButtonPressCommand(IpcProtocol.Version, actionKey);
        ButtonPressed?.Invoke(this, command);

        await WriteResponseAsync(context, 200, new ActionTriggerResponse(true, null));
    }

    private async Task HandlePutStatus(HttpListenerContext context, RouteMatch match, CancellationToken ct)
    {
        try
        {
            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(ct);
            if (!string.IsNullOrWhiteSpace(body))
            {
                var status = JsonSerializer.Deserialize<StreamControllerStatus>(body, HttpJsonSerializer.Options);
                if (status is not null)
                {
                    _lastClientStatus = status.Status;
                    _lastHeartbeat = DateTimeOffset.UtcNow;
                }
            }

            await WriteResponseAsync(context, 204, null);
        }
        catch (JsonException)
        {
            await WriteResponseAsync(context, 400, new { error = "invalid_json" });
        }
    }

    private async Task HandleHealth(HttpListenerContext context, RouteMatch match, CancellationToken ct)
    {
        var health = new
        {
            status = "ok",
            protocolVersion = IpcProtocol.Version,
            lastClientStatus = _lastClientStatus,
            lastHeartbeat = _lastHeartbeat == DateTimeOffset.MinValue ? null : _lastHeartbeat.ToString("o")
        };
        await WriteResponseAsync(context, 200, health);
    }

    private static async Task WriteResponseAsync(HttpListenerContext context, int statusCode, object? body)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        if (body is null)
        {
            context.Response.ContentLength64 = 0;
            return;
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(body, HttpJsonSerializer.Options);
        context.Response.ContentLength64 = json.Length;
        await context.Response.OutputStream.WriteAsync(json);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        try
        {
            if (_listener.IsListening)
                _listener.Stop();
            _listener.Close();
        }
        catch
        {
            // Ignore during disposal
        }
    }
}
