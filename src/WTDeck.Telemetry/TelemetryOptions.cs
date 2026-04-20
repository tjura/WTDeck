namespace WTDeck.Telemetry;

public sealed class TelemetryOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8111";
    public int PollIntervalMs { get; set; } = 100;
    public int HttpTimeoutMs { get; set; } = 2000;
}
