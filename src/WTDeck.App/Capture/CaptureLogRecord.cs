using System.Text;
using System.Text.Json;

namespace WTDeck.App.Capture;

internal sealed class CaptureLogRecord
{
    public DateTimeOffset CapturedAt { get; init; }
    public long ElapsedMs { get; init; }
    public string Kind { get; init; } = "";
    public string? Endpoint { get; init; }
    public string? Path { get; init; }
    public int? Status { get; init; }
    public string? Hash { get; init; }
    public JsonElement Body { get; init; }
    public string? Error { get; init; }
    public string? Marker { get; init; }

    public bool HasBody => Body.ValueKind is not JsonValueKind.Undefined;

    public string ToJsonLine()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("capturedAt", CapturedAt);
            writer.WriteNumber("elapsedMs", ElapsedMs);
            writer.WriteString("kind", Kind);

            if (Endpoint is not null)
                writer.WriteString("endpoint", Endpoint);
            if (Path is not null)
                writer.WriteString("path", Path);
            if (Status.HasValue)
                writer.WriteNumber("status", Status.Value);
            if (Hash is not null)
                writer.WriteString("hash", Hash);
            if (Error is not null)
                writer.WriteString("error", Error);
            if (Marker is not null)
                writer.WriteString("marker", Marker);
            if (HasBody)
            {
                writer.WritePropertyName("body");
                Body.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static CaptureLogRecord FromJsonLine(string line)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;

        return new CaptureLogRecord
        {
            CapturedAt = root.TryGetProperty("capturedAt", out var capturedAt)
                ? capturedAt.GetDateTimeOffset()
                : DateTimeOffset.MinValue,
            ElapsedMs = root.TryGetProperty("elapsedMs", out var elapsedMs) ? elapsedMs.GetInt64() : 0,
            Kind = root.TryGetProperty("kind", out var kind) ? kind.GetString() ?? "" : "",
            Endpoint = root.TryGetProperty("endpoint", out var endpoint) ? endpoint.GetString() : null,
            Path = root.TryGetProperty("path", out var path) ? path.GetString() : null,
            Status = root.TryGetProperty("status", out var status) ? status.GetInt32() : null,
            Hash = root.TryGetProperty("hash", out var hash) ? hash.GetString() : null,
            Error = root.TryGetProperty("error", out var error) ? error.GetString() : null,
            Marker = root.TryGetProperty("marker", out var marker) ? marker.GetString() : null,
            Body = root.TryGetProperty("body", out var body) ? body.Clone() : default
        };
    }
}
