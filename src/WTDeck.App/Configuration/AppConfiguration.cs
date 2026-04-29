using Microsoft.Extensions.Configuration;
using WTDeck.Ipc.Http;
using WTDeck.StreamDock.Configuration;
using WTDeck.Telemetry;

namespace WTDeck.App.Configuration;

public sealed record AppConfiguration(
    AppOptions AppOptions,
    TelemetryOptions TelemetryOptions,
    HttpPluginBridgeOptions HttpPluginBridgeOptions,
    StreamDockOptions StreamDockOptions)
{
    public TelemetryOptions BuildRuntimeTelemetryOptions(int? pollIntervalOverrideMs = null) => new()
    {
        BaseUrl = TelemetryOptions.BaseUrl,
        PollIntervalMs = pollIntervalOverrideMs ?? TelemetryOptions.PollIntervalMs,
        HttpTimeoutMs = TelemetryOptions.HttpTimeoutMs
    };
}

public static class AppConfigurationLoader
{
    public static AppConfiguration Load(IConfiguration configuration)
    {
        var appOptions = new AppOptions();
        configuration.GetSection("App").Bind(appOptions);

        var telemetryOptions = new TelemetryOptions();
        configuration.GetSection("Telemetry").Bind(telemetryOptions);

        var httpOptions = new HttpPluginBridgeOptions();
        configuration.GetSection("Ipc").Bind(httpOptions);

        var streamDockOptions = new StreamDockOptions();
        configuration.GetSection("StreamDock").Bind(streamDockOptions);

        appOptions.TelemetryBaseUrl = telemetryOptions.BaseUrl;
        appOptions.PollIntervalMs = telemetryOptions.PollIntervalMs;
        appOptions.HttpTimeoutMs = telemetryOptions.HttpTimeoutMs;
        appOptions.HttpPort = httpOptions.Port;
        appOptions.HttpBindAddress = httpOptions.BindAddress;

        return new AppConfiguration(appOptions, telemetryOptions, httpOptions, streamDockOptions);
    }
}
