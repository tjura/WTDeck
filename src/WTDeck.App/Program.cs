using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WTDeck.App;
using WTDeck.App.Configuration;
using WTDeck.App.Debug;
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

var parseResult = CommandLineOptionsParser.Parse(args);
if (parseResult.ShowHelp)
{
    Console.WriteLine(CommandLineOptionsParser.UsageText);
    return;
}

if (!parseResult.IsSuccess || parseResult.Options is null)
{
    Console.Error.WriteLine(parseResult.Error ?? "Failed to parse command line arguments.");
    Console.Error.WriteLine();
    Console.Error.WriteLine(CommandLineOptionsParser.UsageText);
    Environment.ExitCode = 1;
    return;
}

var runtimeMode = parseResult.Options;
TelemetryScenarioFile? scenario = null;
if (runtimeMode.EmulateApi)
{
    try
    {
        scenario = TelemetryScenarioFile.LoadFromFile(runtimeMode.ScenarioPath!);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to load scenario '{runtimeMode.ScenarioPath}': {ex.Message}");
        Environment.ExitCode = 1;
        return;
    }
}

var builder = Host.CreateApplicationBuilder(args);

var appConfiguration = AppConfigurationLoader.Load(builder.Configuration);
var appOptions = appConfiguration.AppOptions;

// Auto-detect game folder
appOptions.GameFolder ??= GameFolderDetector.Detect();
if (appOptions.GameFolder is not null)
    Console.WriteLine($"War Thunder detected: {appOptions.GameFolder}");
else
    Console.WriteLine("War Thunder installation not found (telemetry still available at localhost:8111)");

// Auto-detect and parse key bindings. Scenario mode deliberately uses the
// built-in defaults so command validation is deterministic across machines.
IKeyBindingProvider keyBindingProvider;
if (runtimeMode.EmulateApi)
{
    Console.WriteLine("Scenario mode: using default key bindings (G for gear)");
    keyBindingProvider = BlkKeyBindingProvider.FromReader(new StringReader(""));
}
else
{
    var savesDir = appOptions.SavesFolder ?? BlkFileDetector.GetDefaultSavesDirectory();
    var blkFile = appOptions.BlkFilePath ?? (savesDir is not null ? BlkFileDetector.FindBestBlkFile(savesDir) : null);

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
var telemetryOptions = appConfiguration.BuildRuntimeTelemetryOptions(scenario?.StepIntervalMs);

builder.Services.AddSingleton(runtimeMode);
builder.Services.AddSingleton<DebugRunState>();
builder.Services.AddSingleton(appOptions);
builder.Services.AddSingleton(telemetryOptions);

if (runtimeMode.EmulateApi)
{
    builder.Services.AddSingleton(scenario!);
    builder.Services.AddSingleton<ScenarioTelemetrySource>();
    builder.Services.AddSingleton<ITelemetrySource>(sp => sp.GetRequiredService<ScenarioTelemetrySource>());
}
else
{
    builder.Services.AddHttpClient<WarThunderTelemetrySource>();
    builder.Services.AddSingleton<WarThunderTelemetrySource>();
    builder.Services.AddSingleton<ITelemetrySource>(sp => sp.GetRequiredService<WarThunderTelemetrySource>());
}

builder.Services.AddSingleton<TelemetryPollingService>();

// Core - key bindings + input
builder.Services.AddSingleton<IKeyBindingProvider>(keyBindingProvider);
if (runtimeMode.DisableSideEffects)
{
    builder.Services.AddSingleton<NullKeyboardSender>();
    builder.Services.AddSingleton<IKeyboardSender>(sp => sp.GetRequiredService<NullKeyboardSender>());
}
else
{
    builder.Services.AddSingleton<IKeyboardSender, WindowsKeyboardSender>();
}

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

// HTTP IPC bridge (replaces named pipes)
if (runtimeMode.DisableSideEffects)
{
    builder.Services.AddSingleton<RecordingPluginBridge>();
    builder.Services.AddSingleton<IPluginBridge>(sp => sp.GetRequiredService<RecordingPluginBridge>());
}
else
{
    builder.Services.AddSingleton(appConfiguration.HttpPluginBridgeOptions);
    builder.Services.AddSingleton<HttpPluginBridge>();
    builder.Services.AddSingleton<IPluginBridge>(sp => sp.GetRequiredService<HttpPluginBridge>());
}

// StreamDock sync service (installs plugin, creates profile, restarts Stream Controller)
var streamDockOptions = appConfiguration.StreamDockOptions;
builder.Services.AddSingleton(streamDockOptions);
builder.Services.AddSingleton(sp => new StreamDockPaths(sp.GetRequiredService<StreamDockOptions>()));
builder.Services.AddSingleton<PluginAssetInstaller>();
builder.Services.AddSingleton<ProfileManifestBuilder>();
builder.Services.AddSingleton<ProfileInstaller>();
builder.Services.AddSingleton<StreamDockProcessController>();
if (runtimeMode.DisableSideEffects)
    builder.Services.AddSingleton<IPluginSyncService, NoOpPluginSyncService>();
else
    builder.Services.AddSingleton<IPluginSyncService, PluginSyncService>();

// Host
builder.Services.AddHostedService<AppHost>();
if (runtimeMode.DebugEnabled)
    builder.Services.AddHostedService<DebugValidationHostedService>();

var host = builder.Build();

if (runtimeMode.UseTray)
{
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
}
else
{
    var debugRunState = host.Services.GetRequiredService<DebugRunState>();
    await host.RunAsync();
    Environment.ExitCode = debugRunState.ExitCode;
}
