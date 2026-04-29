using WTDeck.Core.Contracts;

namespace WTDeck.App.Configuration;

public sealed class AppOptions
{
    public string? GameFolder { get; set; }
    public string? SavesFolder { get; set; }
    public string? BlkFilePath { get; set; }
    public string TelemetryBaseUrl { get; set; } = "http://localhost:8111";
    public int PollIntervalMs { get; set; } = 100;
    public int HttpTimeoutMs { get; set; } = 2000;
    public int HttpPort { get; set; } = IpcProtocol.HttpPort;
    public string HttpBindAddress { get; set; } = IpcProtocol.HttpBindAddress;
}
