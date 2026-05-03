using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WTDeck.Core.Models;
using WTDeck.Input.Windows;

var logSink = new FileLogSink(Path.Combine(AppContext.BaseDirectory, "logs", "native-gear.log"));
try
{
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    var options = PluginLaunchOptions.Parse(args);
    if (!options.IsValid)
    {
        logSink.Write(LogLevel.Error, $"Invalid StreamDock launch parameters: {string.Join(" ", args)}");
        return 2;
    }

    var plugin = new NativeGearPlugin(
        options,
        new WindowsKeyboardSender(new FileLogger<WindowsKeyboardSender>(logSink)),
        logSink);

    await plugin.RunAsync(cts.Token);
    return 0;
}
catch (OperationCanceledException)
{
    return 0;
}
catch (Exception ex)
{
    logSink.Write(LogLevel.Error, "Fatal native gear plugin error", ex);
    return 1;
}

internal sealed class NativeGearPlugin
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PluginLaunchOptions _options;
    private readonly WindowsKeyboardSender _keyboardSender;
    private readonly FileLogSink _log;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, ContextState> _contexts = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private ClientWebSocket? _webSocket;
    private double? _lastGearPercent;

    public NativeGearPlugin(PluginLaunchOptions options, WindowsKeyboardSender keyboardSender, FileLogSink log)
    {
        _options = options;
        _keyboardSender = keyboardSender;
        _log = log;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(500) };
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _log.Write(LogLevel.Information, $"Starting native gear plugin on StreamDock port {_options.Port}");

        _webSocket = new ClientWebSocket();
        await _webSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{_options.Port}"), ct);
        await SendAsync(new Dictionary<string, object?>
        {
            ["event"] = _options.RegisterEvent,
            ["uuid"] = _options.PluginUuid
        }, ct);

        _log.Write(LogLevel.Information, "Registered with StreamDock host");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var receiveTask = ReceiveLoopAsync(linkedCts.Token);
        var pollTask = PollLoopAsync(linkedCts.Token);

        try
        {
            await receiveTask;
        }
        catch (WebSocketException ex) when (IsExpectedHostDisconnect(ex))
        {
            _log.Write(LogLevel.Information, "StreamDock host closed native gear plugin socket");
        }
        finally
        {
            linkedCts.Cancel();
        }

        try
        {
            await pollTask;
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown after the StreamDock host closes the plugin socket.
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        if (_webSocket is null)
            return;

        var buffer = new byte[8192];
        while (!ct.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await _webSocket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    return;

                ms.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            var message = Encoding.UTF8.GetString(ms.ToArray());
            await HandleMessageAsync(message, ct);
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(PluginConstants.PollIntervalMs));
        while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
        {
            if (_contexts.IsEmpty)
                continue;

            try
            {
                await SyncGearStateAsync(ct);
            }
            catch (Exception ex)
            {
                _log.Write(LogLevel.Warning, "Native gear state sync failed", ex);
                await ApplyStateAsync(GearActionState.Unavailable(), ct);
            }
        }
    }

    private async Task HandleMessageAsync(string message, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;
            var eventName = GetString(root, "event");
            var action = GetString(root, "action");
            var context = GetString(root, "context");

            if (!string.Equals(action, PluginConstants.ActionUuid, StringComparison.Ordinal))
                return;

            switch (eventName)
            {
                case "willAppear" when !string.IsNullOrWhiteSpace(context):
                    _contexts.TryAdd(context, new ContextState());
                    _log.Write(LogLevel.Information, $"Native gear context appeared: {context}");
                    await SyncGearStateAsync(ct);
                    break;

                case "willDisappear" when !string.IsNullOrWhiteSpace(context):
                    _contexts.TryRemove(context, out _);
                    _log.Write(LogLevel.Information, $"Native gear context disappeared: {context}");
                    break;

                case "keyDown" when !string.IsNullOrWhiteSpace(context):
                    await TriggerGearAsync(context, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.Write(LogLevel.Warning, $"Could not handle host message: {message}", ex);
        }
    }

    private async Task TriggerGearAsync(string context, CancellationToken ct)
    {
        try
        {
            _keyboardSender.Send(new KeyChord(new[] { PluginConstants.GearScanCode }));
            await SendSimpleEventAsync("showOk", context, ct);
            _log.Write(LogLevel.Information, "Sent native landing gear key");
        }
        catch (Exception ex)
        {
            _log.Write(LogLevel.Warning, "Failed to send native landing gear key", ex);
            await SendSimpleEventAsync("showAlert", context, ct);
        }
    }

    private async Task SyncGearStateAsync(CancellationToken ct)
    {
        var stateBody = await GetJsonOrNullAsync($"{PluginConstants.TelemetryBaseUrl}/state", ct);
        var indicatorsBody = await GetJsonOrNullAsync($"{PluginConstants.TelemetryBaseUrl}/indicators", ct);
        try
        {
            var actionState = BuildGearState(stateBody, indicatorsBody);
            await ApplyStateAsync(actionState, ct);
        }
        finally
        {
            stateBody?.Dispose();
            indicatorsBody?.Dispose();
        }
    }

    private async Task<JsonDocument?> GetJsonOrNullAsync(string url, CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch
        {
            return null;
        }
    }

    private GearActionState BuildGearState(JsonDocument? stateBody, JsonDocument? indicatorsBody)
    {
        var gearPercent = TryReadGearPercent(stateBody, indicatorsBody);
        if (gearPercent is null)
        {
            _lastGearPercent = null;
            return GearActionState.Unavailable();
        }

        var previous = _lastGearPercent;
        _lastGearPercent = gearPercent;

        var status = GetGearStatus(gearPercent.Value, previous);
        return new GearActionState(
            status,
            status is "down" or "extending" or "retracting");
    }

    private static double? TryReadGearPercent(JsonDocument? stateBody, JsonDocument? indicatorsBody)
    {
        if (stateBody is not null
            && IsValid(stateBody.RootElement)
            && TryGetNumber(stateBody.RootElement, "gear, %", out var stateGear))
        {
            return ClampPercent(stateGear);
        }

        if (indicatorsBody is not null
            && IsValid(indicatorsBody.RootElement)
            && TryGetNumber(indicatorsBody.RootElement, "gears", out var indicatorGear))
        {
            return ClampPercent(indicatorGear * 100d);
        }

        return null;
    }

    private static bool IsValid(JsonElement root)
    {
        return !root.TryGetProperty("valid", out var valid)
               || valid.ValueKind != JsonValueKind.False;
    }

    private static bool TryGetNumber(JsonElement root, string propertyName, out double value)
    {
        value = 0;
        if (!root.TryGetProperty(propertyName, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out value))
            return true;

        if (property.ValueKind == JsonValueKind.String
            && double.TryParse(property.GetString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return false;
    }

    private static double ClampPercent(double value) => Math.Min(100, Math.Max(0, value));

    private static string GetGearStatus(double gearPercent, double? previous)
    {
        if (gearPercent <= 5) return "up";
        if (gearPercent >= 95) return "down";
        if (previous is not null && gearPercent > previous + 1) return "extending";
        if (previous is not null && gearPercent < previous - 1) return "retracting";
        return "unknown";
    }

    private async Task ApplyStateAsync(GearActionState actionState, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (context, state) in _contexts)
        {
            var assetName = actionState.StatusKey switch
            {
                "up" => "gear-retracted.svg",
                "down" => "gear-deployed.svg",
                "extending" => "gear-deploying.svg",
                "retracting" => "gear-retracting.svg",
                "danger" => "gear-damaged.svg",
                "unavailable" => "gear-disabled.svg",
                _ => "gear-unknown.svg"
            };

            if (actionState.IsBlinking)
            {
                if (now - state.LastBlinkToggle >= TimeSpan.FromMilliseconds(PluginConstants.BlinkIntervalMs))
                {
                    state.BlinkPhaseOn = !state.BlinkPhaseOn;
                    state.LastBlinkToggle = now;
                }

                assetName = state.BlinkPhaseOn ? assetName : "gear-blink-off.svg";
            }
            else
            {
                state.BlinkPhaseOn = true;
                state.LastBlinkToggle = now;
            }

            if (state.LastAssetName == assetName && state.LastStatusKey == actionState.StatusKey)
                continue;

            var dataUrl = AssetCache.GetDataUrl(assetName);
            await SendAsync(new Dictionary<string, object?>
            {
                ["event"] = "setImage",
                ["context"] = context,
                ["payload"] = new Dictionary<string, object?>
                {
                    ["image"] = dataUrl,
                    ["target"] = 0,
                    ["state"] = 0
                }
            }, ct);

            state.LastAssetName = assetName;
            state.LastStatusKey = actionState.StatusKey;
        }
    }

    private async Task SendSimpleEventAsync(string eventName, string context, CancellationToken ct)
    {
        await SendAsync(new Dictionary<string, object?>
        {
            ["event"] = eventName,
            ["context"] = context
        }, ct);
    }

    private async Task SendAsync(object payload, CancellationToken ct)
    {
        if (_webSocket is null || _webSocket.State != WebSocketState.Open)
            return;

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(ct);
        try
        {
            await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool IsExpectedHostDisconnect(WebSocketException ex) =>
        ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely
        || ex.Message.Contains("closed", StringComparison.OrdinalIgnoreCase);
}

internal static class PluginConstants
{
    public const string ActionUuid = "com.wtdeck.nativegear.gear";
    public const string TelemetryBaseUrl = "http://localhost:8111";
    public const int GearScanCode = 34; // War Thunder default G key scan code
    public const int PollIntervalMs = 250;
    public const int BlinkIntervalMs = 500;
}

internal sealed record PluginLaunchOptions(
    string Port,
    string PluginUuid,
    string RegisterEvent,
    string Info)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Port)
        && !string.IsNullOrWhiteSpace(PluginUuid)
        && !string.IsNullOrWhiteSpace(RegisterEvent);

    public static PluginLaunchOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("-", StringComparison.Ordinal) || i + 1 >= args.Length)
                continue;

            values[args[i]] = args[i + 1];
            i++;
        }

        return new PluginLaunchOptions(
            values.GetValueOrDefault("-port") ?? "",
            values.GetValueOrDefault("-pluginUUID") ?? "",
            values.GetValueOrDefault("-registerEvent") ?? "",
            values.GetValueOrDefault("-info") ?? "");
    }
}

internal sealed class ContextState
{
    public string? LastStatusKey { get; set; }
    public string? LastAssetName { get; set; }
    public bool BlinkPhaseOn { get; set; } = true;
    public DateTimeOffset LastBlinkToggle { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed record GearActionState(string StatusKey, bool IsBlinking)
{
    public static GearActionState Unavailable() => new("unavailable", false);
}

internal static class AssetCache
{
    private static readonly ConcurrentDictionary<string, string> DataUrls = new(StringComparer.Ordinal);

    public static string GetDataUrl(string assetName)
    {
        return DataUrls.GetOrAdd(assetName, static name =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "assets", name);
            var bytes = File.ReadAllBytes(path);
            return $"data:image/svg+xml;base64,{Convert.ToBase64String(bytes)}";
        });
    }
}

internal sealed class FileLogSink
{
    private readonly string _path;
    private readonly object _gate = new();

    public FileLogSink(string path)
    {
        _path = path;
    }

    public void Write(LogLevel level, string message, Exception? exception = null)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var line = $"{DateTimeOffset.Now:O} [{level}] {message}";
        if (exception is not null)
            line += Environment.NewLine + exception;

        lock (_gate)
        {
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }
}

internal sealed class FileLogger<T> : ILogger<T>
{
    private readonly FileLogSink _sink;

    public FileLogger(FileLogSink sink)
    {
        _sink = sink;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        _sink.Write(logLevel, formatter(state, exception), exception);
    }
}
