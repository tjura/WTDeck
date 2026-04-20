using Microsoft.Extensions.Logging;
using WTDeck.StreamDock.Configuration;
using WTDeck.StreamDock.Interfaces;
using WTDeck.StreamDock.Plugin;
using WTDeck.StreamDock.Process;
using WTDeck.StreamDock.Profiles;

namespace WTDeck.StreamDock.Sync;

public sealed class PluginSyncService : IPluginSyncService
{
    private readonly StreamDockOptions _options;
    private readonly StreamDockPaths _paths;
    private readonly PluginAssetInstaller _pluginInstaller;
    private readonly ProfileInstaller _profileInstaller;
    private readonly StreamDockProcessController _processController;
    private readonly ILogger<PluginSyncService> _logger;

    public PluginSyncService(
        StreamDockOptions options,
        StreamDockPaths paths,
        PluginAssetInstaller pluginInstaller,
        ProfileInstaller profileInstaller,
        StreamDockProcessController processController,
        ILogger<PluginSyncService> logger)
    {
        _options = options;
        _paths = paths;
        _pluginInstaller = pluginInstaller;
        _profileInstaller = profileInstaller;
        _processController = processController;
        _logger = logger;
    }

    public async Task<SyncResult> EnsureInstalledAsync(CancellationToken ct)
    {
        if (!_options.SyncOnStartup)
        {
            _logger.LogInformation("StreamDock sync disabled (SyncOnStartup=false)");
            return new SyncResult(false, false, false, false);
        }

        if (!_paths.IsInstalled)
        {
            var warning = $"StreamDock not found at {_paths.UserDataRoot}. Skipping sync.";
            _logger.LogWarning(warning);
            return new SyncResult(false, false, false, false, warning);
        }

        _logger.LogInformation("Starting StreamDock sync at {Root}", _paths.UserDataRoot);

        // Stop Stream Controller BEFORE writing files to avoid file-in-use errors
        // and to ensure StreamDock re-reads the plugin on restart.
        var wasRunning = _processController.IsRunning();
        if (wasRunning)
        {
            _logger.LogInformation("Stopping Stream Controller before sync");
            await _processController.StopAsync(ct);
        }

        // Install plugin
        var pluginChanged = await _pluginInstaller.InstallAsync(_paths, _options, ct);

        // Install profile
        var profileChanged = await _profileInstaller.EnsureProfileAsync(_paths, _options, ct);

        // Always restart per user decision
        var shouldRestart = _options.AlwaysRestart || wasRunning || pluginChanged || profileChanged;
        if (shouldRestart)
        {
            _logger.LogInformation("Starting Stream Controller");
            await _processController.StartAsync(_paths, ct);
        }

        _logger.LogInformation(
            "StreamDock sync complete (plugin changed: {Plugin}, profile changed: {Profile}, restarted: {Restart})",
            pluginChanged, profileChanged, shouldRestart);

        return new SyncResult(true, pluginChanged, profileChanged, shouldRestart);
    }
}
