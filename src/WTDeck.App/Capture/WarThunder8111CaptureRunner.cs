using System.Diagnostics;
using System.Net;
using System.Text.Json;
using WTDeck.App.Configuration;

namespace WTDeck.App.Capture;

public sealed class WarThunder8111CaptureRunner
{
    private const string DefaultMarker = "missile_visible_or_warning_seen";
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly TextWriter _output;

    private int _lastHudEventId;
    private int _lastHudDamageId;
    private int _lastGameChatId;

    public WarThunder8111CaptureRunner(HttpClient httpClient, string baseUrl, TextWriter? output = null)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _output = output ?? Console.Out;
    }

    public async Task<int> RunAsync(Capture8111Options options, CancellationToken ct)
    {
        var outputDirectory = ResolveOutputDirectory(options.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var segmentRecords = new List<CaptureLogRecord>();
        var lastKeyByEndpoint = new Dictionary<string, string>(StringComparer.Ordinal);
        var segmentNumber = 1;
        var stopwatch = Stopwatch.StartNew();
        var startedAt = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromSeconds(options.DurationSeconds);
        var pollInterval = TimeSpan.FromMilliseconds(options.IntervalMs);
        var dumpInterval = TimeSpan.FromSeconds(options.DumpIntervalSeconds);
        var nextDumpAt = dumpInterval;
        var stopRequested = false;

        await _output.WriteLineAsync($"Capturing War Thunder localhost API to {outputDirectory}");
        await _output.WriteLineAsync("Press 'm' when missile warning/marker is visible. Press 'q' to stop.");

        while (!ct.IsCancellationRequested && !stopRequested && stopwatch.Elapsed < duration)
        {
            var tickStarted = stopwatch.Elapsed;
            var samples = await PollAllEndpointsAsync(startedAt, stopwatch.ElapsedMilliseconds, ct);

            foreach (var sample in samples)
            {
                if (lastKeyByEndpoint.TryGetValue(sample.Endpoint.Name, out var lastKey) && lastKey == sample.ChangeKey)
                    continue;

                lastKeyByEndpoint[sample.Endpoint.Name] = sample.ChangeKey;
                segmentRecords.Add(sample.ToLogRecord());
            }

            if (TryReadCommandKey(out var key))
            {
                if (key == 'm')
                {
                    segmentRecords.Add(new CaptureLogRecord
                    {
                        CapturedAt = DateTimeOffset.UtcNow,
                        ElapsedMs = stopwatch.ElapsedMilliseconds,
                        Kind = "marker",
                        Marker = DefaultMarker
                    });
                    await _output.WriteLineAsync($"Marker written at {stopwatch.Elapsed.TotalSeconds:0.0}s");
                }
                else if (key == 'q')
                {
                    stopRequested = true;
                }
            }

            if (stopwatch.Elapsed >= nextDumpAt)
            {
                await FlushSegmentAsync(outputDirectory, segmentNumber++, segmentRecords, ct);
                segmentRecords.Clear();
                nextDumpAt += dumpInterval;
            }

            var delay = pollInterval - (stopwatch.Elapsed - tickStarted);
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);
        }

        await FlushSegmentAsync(outputDirectory, segmentNumber, segmentRecords, ct);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "capture-info.json"),
            JsonSerializer.Serialize(new
            {
                startedAt,
                completedAt = DateTimeOffset.UtcNow,
                baseUrl = _baseUrl,
                options.DurationSeconds,
                options.IntervalMs,
                options.DumpIntervalSeconds
            }, new JsonSerializerOptions { WriteIndented = true }),
            ct);

        await _output.WriteLineAsync($"Capture complete: {outputDirectory}");
        return 0;
    }

    private async Task<IReadOnlyList<EndpointSample>> PollAllEndpointsAsync(
        DateTimeOffset startedAt,
        long elapsedMs,
        CancellationToken ct)
    {
        var endpoints = BuildEndpoints();
        var tasks = endpoints.Select(endpoint => FetchEndpointAsync(endpoint, startedAt, elapsedMs, ct)).ToArray();
        return await Task.WhenAll(tasks);
    }

    private IReadOnlyList<CaptureEndpoint> BuildEndpoints() =>
    [
        new("/indicators", "/indicators"),
        new("/state", "/state"),
        new("/hudmsg", $"/hudmsg?lastEvt={_lastHudEventId}&lastDmg={_lastHudDamageId}"),
        new("/gamechat", $"/gamechat?lastId={_lastGameChatId}"),
        new("/map_obj.json", "/map_obj.json"),
        new("/map_info.json", "/map_info.json"),
        new("/mission.json", "/mission.json")
    ];

    private async Task<EndpointSample> FetchEndpointAsync(
        CaptureEndpoint endpoint,
        DateTimeOffset startedAt,
        long elapsedMs,
        CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.GetAsync(_baseUrl + endpoint.Path, ct);
            var bodyText = await response.Content.ReadAsStringAsync(ct);
            var status = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                return EndpointSample.ForError(startedAt, elapsedMs, endpoint, status, response.StatusCode.ToString());
            }

            if (string.IsNullOrWhiteSpace(bodyText))
                return EndpointSample.ForEmpty(startedAt, elapsedMs, endpoint, status);

            using var document = JsonDocument.Parse(bodyText);
            var body = document.RootElement.Clone();
            UpdateCursors(endpoint.Name, body);
            var normalized = JsonCanonicalizer.Normalize(body);
            return EndpointSample.ForBody(startedAt, elapsedMs, endpoint, status, body, JsonCanonicalizer.Hash(normalized));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return EndpointSample.ForError(startedAt, elapsedMs, endpoint, null, "timeout");
        }
        catch (HttpRequestException ex)
        {
            return EndpointSample.ForError(startedAt, elapsedMs, endpoint, null, ex.StatusCode?.ToString() ?? ex.GetType().Name);
        }
        catch (JsonException)
        {
            return EndpointSample.ForError(startedAt, elapsedMs, endpoint, (int)HttpStatusCode.OK, "invalid_json");
        }
    }

    private void UpdateCursors(string endpointName, JsonElement body)
    {
        if (endpointName == "/hudmsg")
        {
            _lastHudEventId = Math.Max(_lastHudEventId, ExtractMaxInt(body, "lastEvt", "eventId", "evtId", "id"));
            _lastHudDamageId = Math.Max(_lastHudDamageId, ExtractMaxInt(body, "lastDmg", "damageId", "dmgId"));
        }
        else if (endpointName == "/gamechat")
        {
            _lastGameChatId = Math.Max(_lastGameChatId, ExtractMaxInt(body, "lastId", "id"));
        }
    }

    private static int ExtractMaxInt(JsonElement element, params string[] propertyNames)
    {
        var max = 0;
        ExtractMaxIntRecursive(element, propertyNames, ref max);
        return max;
    }

    private static void ExtractMaxIntRecursive(JsonElement element, IReadOnlyCollection<string> propertyNames, ref int max)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (propertyNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.Number
                    && property.Value.TryGetInt32(out var value))
                {
                    max = Math.Max(max, value);
                }

                ExtractMaxIntRecursive(property.Value, propertyNames, ref max);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                ExtractMaxIntRecursive(item, propertyNames, ref max);
        }
    }

    private static async Task FlushSegmentAsync(
        string outputDirectory,
        int segmentNumber,
        IReadOnlyList<CaptureLogRecord> records,
        CancellationToken ct)
    {
        if (records.Count == 0)
            return;

        var path = Path.Combine(outputDirectory, $"segment-{segmentNumber:0000}.jsonl");
        await using var stream = File.Create(path);
        await using var writer = new StreamWriter(stream);
        foreach (var record in records)
            await writer.WriteLineAsync(record.ToJsonLine().AsMemory(), ct);
    }

    private static bool TryReadCommandKey(out char key)
    {
        key = '\0';
        try
        {
            if (!Console.KeyAvailable)
                return false;

            key = char.ToLowerInvariant(Console.ReadKey(intercept: true).KeyChar);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string ResolveOutputDirectory(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        return Path.GetFullPath(Path.Combine(
            "tmp",
            "8111-captures",
            DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss")));
    }

    private sealed record CaptureEndpoint(string Name, string Path);

    private sealed record EndpointSample(
        DateTimeOffset CapturedAt,
        long ElapsedMs,
        CaptureEndpoint Endpoint,
        int? Status,
        string ChangeKey,
        JsonElement Body,
        string? Hash,
        string? Error)
    {
        public static EndpointSample ForBody(
            DateTimeOffset capturedAt,
            long elapsedMs,
            CaptureEndpoint endpoint,
            int status,
            JsonElement body,
            string hash)
            => new(capturedAt, elapsedMs, endpoint, status, $"body:{hash}", body, hash, null);

        public static EndpointSample ForEmpty(
            DateTimeOffset capturedAt,
            long elapsedMs,
            CaptureEndpoint endpoint,
            int status)
            => new(capturedAt, elapsedMs, endpoint, status, $"empty:{status}", default, null, null);

        public static EndpointSample ForError(
            DateTimeOffset capturedAt,
            long elapsedMs,
            CaptureEndpoint endpoint,
            int? status,
            string error)
            => new(capturedAt, elapsedMs, endpoint, status, $"error:{status}:{error}", default, null, error);

        public CaptureLogRecord ToLogRecord() => new()
        {
            CapturedAt = CapturedAt,
            ElapsedMs = ElapsedMs,
            Kind = "sample",
            Endpoint = Endpoint.Name,
            Path = Endpoint.Path,
            Status = Status,
            Hash = Hash,
            Body = Body,
            Error = Error
        };
    }
}
