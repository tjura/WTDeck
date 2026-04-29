using FluentAssertions;
using Microsoft.Extensions.Configuration;
using WTDeck.App.Configuration;
using WTDeck.Core.Contracts;

namespace WTDeck.App.IntegrationTests.Configuration;

public sealed class AppConfigurationLoaderTests
{
    [Fact]
    public void Load_without_sections_uses_runtime_defaults()
    {
        var configuration = new ConfigurationBuilder().Build();

        var result = AppConfigurationLoader.Load(configuration);

        result.TelemetryOptions.BaseUrl.Should().Be("http://localhost:8111");
        result.TelemetryOptions.PollIntervalMs.Should().Be(100);
        result.TelemetryOptions.HttpTimeoutMs.Should().Be(2000);
        result.HttpPluginBridgeOptions.Port.Should().Be(IpcProtocol.HttpPort);
        result.HttpPluginBridgeOptions.BindAddress.Should().Be(IpcProtocol.HttpBindAddress);
        result.StreamDockOptions.SyncOnStartup.Should().BeTrue();
        result.StreamDockOptions.AlwaysRestart.Should().BeTrue();
        result.StreamDockOptions.ForceOverwriteProfile.Should().BeTrue();
    }

    [Fact]
    public void Load_binds_documented_telemetry_ipc_and_streamdock_settings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telemetry:BaseUrl"] = "http://127.0.0.1:9000",
                ["Telemetry:PollIntervalMs"] = "250",
                ["Telemetry:HttpTimeoutMs"] = "1500",
                ["Ipc:Port"] = "9876",
                ["Ipc:BindAddress"] = "127.0.0.2",
                ["StreamDock:SyncOnStartup"] = "false",
                ["StreamDock:AlwaysRestart"] = "false",
                ["StreamDock:ForceOverwriteProfile"] = "false",
                ["StreamDock:UserDataRoot"] = @"C:\StreamDockData",
                ["StreamDock:InstallDir"] = @"C:\StreamController",
                ["StreamDock:DeviceUUID"] = "Device",
                ["StreamDock:DeviceSerialNumber"] = "Serial",
                ["StreamDock:DeviceModel"] = "Model",
                ["StreamDock:ProfileName"] = "Profile",
                ["StreamDock:PluginUuid"] = "plugin.uuid",
                ["StreamDock:PluginActionUuid"] = "plugin.action",
                ["StreamDock:PluginFlaresActionUuid"] = "plugin.flares",
                ["StreamDock:PluginFlightAlertsActionUuid"] = "plugin.flight-alerts",
                ["StreamDock:FlightAlertsPanelSlot"] = "5,0"
            })
            .Build();

        var result = AppConfigurationLoader.Load(configuration);

        result.TelemetryOptions.BaseUrl.Should().Be("http://127.0.0.1:9000");
        result.TelemetryOptions.PollIntervalMs.Should().Be(250);
        result.TelemetryOptions.HttpTimeoutMs.Should().Be(1500);
        result.HttpPluginBridgeOptions.Port.Should().Be(9876);
        result.HttpPluginBridgeOptions.BindAddress.Should().Be("127.0.0.2");
        result.AppOptions.TelemetryBaseUrl.Should().Be(result.TelemetryOptions.BaseUrl);
        result.AppOptions.PollIntervalMs.Should().Be(result.TelemetryOptions.PollIntervalMs);
        result.AppOptions.HttpTimeoutMs.Should().Be(result.TelemetryOptions.HttpTimeoutMs);
        result.AppOptions.HttpPort.Should().Be(result.HttpPluginBridgeOptions.Port);
        result.AppOptions.HttpBindAddress.Should().Be(result.HttpPluginBridgeOptions.BindAddress);
        result.StreamDockOptions.SyncOnStartup.Should().BeFalse();
        result.StreamDockOptions.AlwaysRestart.Should().BeFalse();
        result.StreamDockOptions.ForceOverwriteProfile.Should().BeFalse();
        result.StreamDockOptions.UserDataRoot.Should().Be(@"C:\StreamDockData");
        result.StreamDockOptions.InstallDir.Should().Be(@"C:\StreamController");
        result.StreamDockOptions.DeviceUUID.Should().Be("Device");
        result.StreamDockOptions.DeviceSerialNumber.Should().Be("Serial");
        result.StreamDockOptions.DeviceModel.Should().Be("Model");
        result.StreamDockOptions.ProfileName.Should().Be("Profile");
        result.StreamDockOptions.PluginUuid.Should().Be("plugin.uuid");
        result.StreamDockOptions.PluginActionUuid.Should().Be("plugin.action");
        result.StreamDockOptions.PluginFlaresActionUuid.Should().Be("plugin.flares");
        result.StreamDockOptions.PluginFlightAlertsActionUuid.Should().Be("plugin.flight-alerts");
        result.StreamDockOptions.FlightAlertsPanelSlot.Should().Be("5,0");
    }

    [Fact]
    public void BuildRuntimeTelemetryOptions_overrides_poll_interval_for_scenario_mode()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telemetry:BaseUrl"] = "http://127.0.0.1:9000",
                ["Telemetry:PollIntervalMs"] = "250",
                ["Telemetry:HttpTimeoutMs"] = "1500"
            })
            .Build();

        var result = AppConfigurationLoader.Load(configuration)
            .BuildRuntimeTelemetryOptions(pollIntervalOverrideMs: 75);

        result.BaseUrl.Should().Be("http://127.0.0.1:9000");
        result.PollIntervalMs.Should().Be(75);
        result.HttpTimeoutMs.Should().Be(1500);
    }
}
