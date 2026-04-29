using System.Text.Json;

namespace WTDeck.App.Capture;

public sealed class WarThunder8111CaptureAnalyzer
{
    private static readonly string[] InterestingTerms =
    [
        "missile",
        "msl",
        "launch",
        "warning",
        "rwr",
        "radar",
        "lock",
        "threat",
        "weapon"
    ];

    private readonly TextWriter _output;

    public WarThunder8111CaptureAnalyzer(TextWriter? output = null)
    {
        _output = output ?? Console.Out;
    }

    public async Task<int> AnalyzeAsync(string captureDirectory, CancellationToken ct)
    {
        if (!Directory.Exists(captureDirectory))
        {
            await _output.WriteLineAsync($"Capture directory not found: {captureDirectory}");
            return 1;
        }

        var records = LoadRecords(captureDirectory);
        var samples = records.Where(r => r.Kind == "sample").OrderBy(r => r.ElapsedMs).ToList();
        var markers = records.Where(r => r.Kind == "marker").OrderBy(r => r.ElapsedMs).ToList();

        var windows = markers.Count > 0
            ? markers.Select((marker, index) => AnalyzeWindow($"marker-{index + 1}", marker.ElapsedMs, samples)).ToList()
            : [AnalyzeWindow("all-records", samples.Count > 0 ? samples[samples.Count / 2].ElapsedMs : 0, samples, useFullSet: true)];

        var analysis = new CaptureAnalysis(
            CaptureDirectory: captureDirectory,
            RecordCount: records.Count,
            SampleCount: samples.Count,
            MarkerCount: markers.Count,
            Windows: windows);

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(
            Path.Combine(captureDirectory, "analysis.json"),
            JsonSerializer.Serialize(analysis, jsonOptions),
            ct);
        await File.WriteAllTextAsync(
            Path.Combine(captureDirectory, "analysis.md"),
            BuildMarkdown(analysis),
            ct);

        await _output.WriteLineAsync($"Analysis written to {Path.Combine(captureDirectory, "analysis.md")}");
        return 0;
    }

    private static List<CaptureLogRecord> LoadRecords(string captureDirectory)
    {
        var records = new List<CaptureLogRecord>();
        foreach (var file in Directory.GetFiles(captureDirectory, "segment-*.jsonl").OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var line in File.ReadLines(file))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    records.Add(CaptureLogRecord.FromJsonLine(line));
            }
        }

        return records;
    }

    private static CaptureAnalysisWindow AnalyzeWindow(
        string name,
        long centerElapsedMs,
        IReadOnlyList<CaptureLogRecord> samples,
        bool useFullSet = false)
    {
        var min = useFullSet ? long.MinValue : centerElapsedMs - 5000;
        var max = useFullSet ? long.MaxValue : centerElapsedMs + 5000;
        var windowSamples = samples
            .Where(r => r.ElapsedMs >= min && r.ElapsedMs <= max)
            .OrderBy(r => r.ElapsedMs)
            .ToList();

        var highlights = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var mapSummaries = new List<MapObjectSummary>();
        var endpointCounts = windowSamples
            .Where(r => r.Endpoint is not null)
            .GroupBy(r => r.Endpoint!, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        foreach (var sample in windowSamples)
        {
            if (sample.HasBody)
                CollectHighlights(sample.Body, "$", highlights);

            if (sample.Endpoint == "/map_obj.json" && sample.HasBody)
                mapSummaries.Add(SummarizeMapObjects(sample));
        }

        return new CaptureAnalysisWindow(
            Name: name,
            CenterElapsedMs: centerElapsedMs,
            SampleCount: windowSamples.Count,
            EndpointCounts: endpointCounts,
            Highlights: highlights.Take(100).ToArray(),
            MapObjectSummaries: mapSummaries);
    }

    private static void CollectHighlights(JsonElement element, string path, ISet<string> highlights)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var nextPath = path == "$" ? "$." + property.Name : path + "." + property.Name;
                    if (InterestingTerms.Any(term => property.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
                        highlights.Add(nextPath);
                    CollectHighlights(property.Value, nextPath, highlights);
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                    CollectHighlights(item, $"{path}[{index++}]", highlights);
                break;

            case JsonValueKind.String:
                var value = element.GetString();
                if (value is not null && InterestingTerms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    highlights.Add($"{path} = {value}");
                break;
        }
    }

    private static MapObjectSummary SummarizeMapObjects(CaptureLogRecord sample)
    {
        var objects = sample.Body.ValueKind == JsonValueKind.Array
            ? sample.Body.EnumerateArray().ToList()
            : [];

        var descriptors = objects
            .Select(DescribeMapObject)
            .Where(static d => !string.IsNullOrWhiteSpace(d))
            .GroupBy(static d => d, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(static g => g.Count())
            .ThenBy(static g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .Select(static g => $"{g.Key} x{g.Count()}")
            .ToArray();

        return new MapObjectSummary(sample.ElapsedMs, objects.Count, descriptors);
    }

    private static string DescribeMapObject(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return "";

        var parts = new[]
        {
            ReadString(item, "type"),
            ReadString(item, "icon"),
            ReadString(item, "icon_bg"),
            ReadString(item, "color"),
            ReadString(item, "blink")
        }.Where(static p => !string.IsNullOrWhiteSpace(p));

        return string.Join(" / ", parts);
    }

    private static string? ReadString(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => $"{propertyName}:{value.GetString()}",
            JsonValueKind.Number => $"{propertyName}:{value.GetRawText()}",
            JsonValueKind.True => $"{propertyName}:true",
            JsonValueKind.False => $"{propertyName}:false",
            _ => null
        };
    }

    private static string BuildMarkdown(CaptureAnalysis analysis)
    {
        using var writer = new StringWriter();
        writer.WriteLine("# 8111 Capture Analysis");
        writer.WriteLine();
        writer.WriteLine($"- Capture directory: `{analysis.CaptureDirectory}`");
        writer.WriteLine($"- Records: {analysis.RecordCount}");
        writer.WriteLine($"- Samples: {analysis.SampleCount}");
        writer.WriteLine($"- Markers: {analysis.MarkerCount}");
        writer.WriteLine();

        foreach (var window in analysis.Windows)
        {
            writer.WriteLine($"## {window.Name}");
            writer.WriteLine();
            writer.WriteLine($"- Center elapsed: {window.CenterElapsedMs} ms");
            writer.WriteLine($"- Window samples: {window.SampleCount}");
            writer.WriteLine("- Endpoint changes:");
            foreach (var (endpoint, count) in window.EndpointCounts)
                writer.WriteLine($"  - `{endpoint}`: {count}");

            writer.WriteLine();
            writer.WriteLine("### Interesting keys/messages");
            if (window.Highlights.Count == 0)
            {
                writer.WriteLine("- None found");
            }
            else
            {
                foreach (var highlight in window.Highlights)
                    writer.WriteLine($"- `{highlight}`");
            }

            writer.WriteLine();
            writer.WriteLine("### Map object summaries");
            if (window.MapObjectSummaries.Count == 0)
            {
                writer.WriteLine("- No `/map_obj.json` samples in window");
            }
            else
            {
                foreach (var summary in window.MapObjectSummaries)
                {
                    writer.WriteLine($"- {summary.ElapsedMs} ms: {summary.ObjectCount} objects");
                    foreach (var descriptor in summary.TopDescriptors)
                        writer.WriteLine($"  - `{descriptor}`");
                }
            }

            writer.WriteLine();
        }

        return writer.ToString();
    }

    private sealed record CaptureAnalysis(
        string CaptureDirectory,
        int RecordCount,
        int SampleCount,
        int MarkerCount,
        IReadOnlyList<CaptureAnalysisWindow> Windows);

    private sealed record CaptureAnalysisWindow(
        string Name,
        long CenterElapsedMs,
        int SampleCount,
        IReadOnlyDictionary<string, int> EndpointCounts,
        IReadOnlyList<string> Highlights,
        IReadOnlyList<MapObjectSummary> MapObjectSummaries);

    private sealed record MapObjectSummary(
        long ElapsedMs,
        int ObjectCount,
        IReadOnlyList<string> TopDescriptors);
}
