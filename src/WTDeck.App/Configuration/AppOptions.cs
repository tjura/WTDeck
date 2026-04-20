namespace WTDeck.App.Configuration;

public sealed class AppOptions
{
    public string? GameFolder { get; set; }
    public string? SavesFolder { get; set; }
    public string? BlkFilePath { get; set; }
    public string TelemetryBaseUrl { get; set; } = "http://localhost:8111";
    public int PollIntervalMs { get; set; } = 100;
    public int HttpPort { get; set; } = 8730;
    public string HttpBindAddress { get; set; } = "127.0.0.1";
}
