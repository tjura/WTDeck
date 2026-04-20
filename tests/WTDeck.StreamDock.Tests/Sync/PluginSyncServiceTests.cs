using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WTDeck.StreamDock.Configuration;
using WTDeck.StreamDock.Plugin;
using WTDeck.StreamDock.Process;
using WTDeck.StreamDock.Profiles;
using WTDeck.StreamDock.Sync;

namespace WTDeck.StreamDock.Tests.Sync;

public class PluginSyncServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public PluginSyncServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wtdeck-sync-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempRoot, "plugins"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "profiles"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task Sync_disabled_returns_without_synced_flag()
    {
        var options = new StreamDockOptions { SyncOnStartup = false };
        var service = CreateService(options);

        var result = await service.EnsureInstalledAsync(CancellationToken.None);

        result.Synced.Should().BeFalse();
    }

    [Fact]
    public async Task Sync_when_streamdock_not_installed_returns_warning()
    {
        var options = new StreamDockOptions
        {
            SyncOnStartup = true,
            UserDataRoot = "C:\\does\\not\\exist",
            InstallDir = "C:\\does\\not\\exist"
        };
        var service = CreateService(options);

        var result = await service.EnsureInstalledAsync(CancellationToken.None);

        result.Synced.Should().BeFalse();
        result.Warning.Should().NotBeNull();
    }

    private PluginSyncService CreateService(StreamDockOptions options)
    {
        var paths = new StreamDockPaths(options);
        var pluginInstaller = new PluginAssetInstaller(NullLogger<PluginAssetInstaller>.Instance);
        var profileInstaller = new ProfileInstaller(
            NullLogger<ProfileInstaller>.Instance,
            new ProfileManifestBuilder());
        var processController = new StreamDockProcessController(
            NullLogger<StreamDockProcessController>.Instance);

        return new PluginSyncService(
            options, paths, pluginInstaller, profileInstaller,
            processController, NullLogger<PluginSyncService>.Instance);
    }
}
