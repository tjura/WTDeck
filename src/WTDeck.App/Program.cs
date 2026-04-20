using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WTDeck.App;
using WTDeck.App.Audio;
using WTDeck.App.Configuration;
using WTDeck.App.Detection;
using WTDeck.App.Tray;
using WTDeck.Core.Alerts;
using WTDeck.Core.Interfaces;
using WTDeck.Core.KeyBindings;
using WTDeck.Core.Profiles;
using WTDeck.Core.Profiles.Aircraft;
using WTDeck.Core.Rules;
using WTDeck.Core.Rules.Gear;
using WTDeck.Input.Windows;
using WTDeck.Ipc.Http;
using WTDeck.StreamDock.Configuration;
using WTDeck.StreamDock.Interfaces;
using WTDeck.StreamDock.Plugin;
using WTDeck.StreamDock.Process;
using WTDeck.StreamDock.Profiles;
using WTDeck.StreamDock.Sync;
using WTDeck.Telemetry;

var builder = Host.CreateApplicationBuilder(args);

// Configuration - read from appsettings.json sections
var appOptions = new AppOptions();
var telemetrySection = builder.Configuration.GetSection("Telemetry");
appOptions.TelemetryBaseUrl = telemetrySection["BaseUrl"] ?? appOptions.TelemetryBaseUrl;
if (int.TryParse(telemetrySection["PollIntervalMs"], out var pollMs))
    appOptions.PollIntervalMs = pollMs;
var ipcSection = builder.Configuration.GetSection("Ipc");
if (int.TryParse(ipcSection["Port"], out var httpPort))
    appOptions.HttpPort = httpPort;
appOptions.HttpBindAddress = ipcSection["BindAddress"] ?? appOptions.HttpBindAddress;
var soundSection = builder.Configuration.GetSection("Sound");
if (bool.TryParse(soundSection["Enabled"], out var soundEnabled))
    appOptions.EnableSound = soundEnabled;
appOptions.PrimaryAlertFile = soundSection["PrimaryAlertFile"] ?? appOptions.PrimaryAlertFile;

// Auto-detect game folder
appOptions.GameFolder ??= GameFolderDetector.Detect();
if (appOptions.GameFolder is not null)
    Console.WriteLine($"War Thunder detected: {appOptions.GameFolder}");
else
    Console.WriteLine("War Thunder installation not found (telemetry still available at localhost:8111)");

// Auto-detect and parse key bindings
var savesDir = appOptions.SavesFolder ?? BlkFileDetector.GetDefaultSavesDirectory();
var blkFile = appOptions.BlkFilePath ?? (savesDir is not null ? BlkFileDetector.FindBestBlkFile(savesDir) : null);

IKeyBindingProvider keyBindingProvider;
if (blkFile is not null)
{
    Console.WriteLine($"Key bindings loaded from: {blkFile}");
    keyBindingProvider = BlkKeyBindingProvider.FromFile(blkFile);
}
else
{
    Console.WriteLine("No key binding file found, using defaults (G for gear)");
    keyBindingProvider = BlkKeyBindingProvider.FromReader(new StringReader(""));
}

// Log detected gear binding
var gearBinding = keyBindingProvider.GetBinding(WTDeck.Core.Models.ActionId.Gear);
if (gearBinding is not null)
{
    var keys = string.Join(" OR ",
        gearBinding.Chords.Select(c =>
            string.Join("+", c.ScanCodes.Select(ScanCodeMap.GetName))));
    Console.WriteLine($"Gear key binding: {keys}");
}

// Telemetry
var telemetryOptions = new TelemetryOptions
{
    BaseUrl = appOptions.TelemetryBaseUrl,
    PollIntervalMs = appOptions.PollIntervalMs
};

builder.Services.AddSingleton(appOptions);
builder.Services.AddSingleton(telemetryOptions);
builder.Services.AddHttpClient<WarThunderTelemetrySource>();
builder.Services.AddSingleton<ITelemetrySource>(sp => sp.GetRequiredService<WarThunderTelemetrySource>());
builder.Services.AddSingleton<TelemetryPollingService>();

// Core - key bindings + input
builder.Services.AddSingleton<IKeyBindingProvider>(keyBindingProvider);
builder.Services.AddSingleton<IKeyboardSender, WindowsKeyboardSender>();

// Core - time + aircraft profiles
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AircraftProfile>(A4NSkyhawkProfile.Instance);
builder.Services.AddSingleton<IAircraftProfileRegistry>(sp =>
    new AircraftProfileRegistry(sp.GetServices<AircraftProfile>()));

// Core - alerts
builder.Services.AddSingleton<IAlertActionBindingRegistry, AlertActionBindingRegistry>();
builder.Services.AddSingleton<IAlertCenter, AlertCenter>();

// Core - rules + rule engine
builder.Services.AddSingleton<IRule, GearButtonRule>();
builder.Services.AddSingleton<IRule, GearOverspeedRule>();
builder.Services.AddSingleton<IRuleEngine, CompositeRuleEngine>();

// Sound
if (appOptions.EnableSound)
    builder.Services.AddSingleton<ISoundAlert, PrimarySoundAlert>();
else
    builder.Services.AddSingleton<ISoundAlert, SilentSoundAlert>();

// HTTP IPC bridge (replaces named pipes)
var httpOptions = new HttpPluginBridgeOptions
{
    Port = appOptions.HttpPort,
    BindAddress = appOptions.HttpBindAddress
};
builder.Services.AddSingleton(httpOptions);
builder.Services.AddSingleton<HttpPluginBridge>();
builder.Services.AddSingleton<IPluginBridge>(sp => sp.GetRequiredService<HttpPluginBridge>());

// StreamDock sync service (installs plugin, creates profile, restarts Stream Controller)
var streamDockOptions = new StreamDockOptions();
var streamDockSection = builder.Configuration.GetSection("StreamDock");
if (bool.TryParse(streamDockSection["SyncOnStartup"], out var syncOnStartup))
    streamDockOptions.SyncOnStartup = syncOnStartup;
streamDockOptions.DeviceUUID = streamDockSection["DeviceUUID"] ?? streamDockOptions.DeviceUUID;
streamDockOptions.DeviceSerialNumber = streamDockSection["DeviceSerialNumber"] ?? streamDockOptions.DeviceSerialNumber;
streamDockOptions.DeviceModel = streamDockSection["DeviceModel"] ?? streamDockOptions.DeviceModel;
streamDockOptions.ProfileName = streamDockSection["ProfileName"] ?? streamDockOptions.ProfileName;
streamDockOptions.PluginUuid = streamDockSection["PluginUuid"] ?? streamDockOptions.PluginUuid;

builder.Services.AddSingleton(streamDockOptions);
builder.Services.AddSingleton(sp => new StreamDockPaths(sp.GetRequiredService<StreamDockOptions>()));
builder.Services.AddSingleton<PluginAssetInstaller>();
builder.Services.AddSingleton<ProfileManifestBuilder>();
builder.Services.AddSingleton<ProfileInstaller>();
builder.Services.AddSingleton<StreamDockProcessController>();
builder.Services.AddSingleton<IPluginSyncService, PluginSyncService>();

// Host
builder.Services.AddHostedService<AppHost>();

var host = builder.Build();

// Tray icon (runs on the main thread)
using var trayManager = new TrayIconManager(
    host.Services.GetRequiredService<ILogger<TrayIconManager>>(),
    () =>
    {
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.StopApplication();
    });

var hostTask = host.RunAsync();

trayManager.Initialize();
System.Windows.Forms.Application.Run();

await hostTask;
