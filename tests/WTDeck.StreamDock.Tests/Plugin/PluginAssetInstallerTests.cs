using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WTDeck.StreamDock.Configuration;
using WTDeck.StreamDock.Plugin;

namespace WTDeck.StreamDock.Tests.Plugin;

public class PluginAssetInstallerTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly StreamDockPaths _paths;
    private readonly StreamDockOptions _options;
    private readonly PluginAssetInstaller _installer;

    public PluginAssetInstallerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wtdeck-plugin-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempRoot, "plugins"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "profiles"));

        _options = new StreamDockOptions
        {
            UserDataRoot = _tempRoot,
            InstallDir = null,
            PluginUuid = "com.wtdeck.streamdock.test"
        };

        _paths = new StreamDockPaths(_options);
        _installer = new PluginAssetInstaller(NullLogger<PluginAssetInstaller>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task Installs_plugin_manifest_and_scripts_to_target_dir()
    {
        var changed = await _installer.InstallAsync(_paths, _options, CancellationToken.None);
        changed.Should().BeTrue();

        var pluginDir = Path.Combine(_paths.PluginsDir, $"{_options.PluginUuid}.sdPlugin");
        Directory.Exists(pluginDir).Should().BeTrue();
        File.Exists(Path.Combine(pluginDir, "manifest.json")).Should().BeTrue();
        File.Exists(Path.Combine(pluginDir, "plugin", "index.html")).Should().BeTrue();
        File.Exists(Path.Combine(pluginDir, "plugin", "index.js")).Should().BeTrue();
    }

    [Fact]
    public async Task Installs_asset_files()
    {
        await _installer.InstallAsync(_paths, _options, CancellationToken.None);

        var pluginDir = Path.Combine(_paths.PluginsDir, $"{_options.PluginUuid}.sdPlugin");
        File.Exists(Path.Combine(pluginDir, "assets", "gear-retracted.svg")).Should().BeTrue();
        File.Exists(Path.Combine(pluginDir, "assets", "gear-damaged.svg")).Should().BeTrue();
        File.Exists(Path.Combine(pluginDir, "assets", "plugin-icon.svg")).Should().BeTrue();
    }

    [Fact]
    public async Task Second_install_with_same_files_is_no_op()
    {
        await _installer.InstallAsync(_paths, _options, CancellationToken.None);
        var second = await _installer.InstallAsync(_paths, _options, CancellationToken.None);

        second.Should().BeFalse();
    }

    [Fact]
    public async Task Reinstall_after_user_edit_overwrites()
    {
        await _installer.InstallAsync(_paths, _options, CancellationToken.None);

        var pluginDir = Path.Combine(_paths.PluginsDir, $"{_options.PluginUuid}.sdPlugin");
        var manifestPath = Path.Combine(pluginDir, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, "{ \"tampered\": true }");

        var changed = await _installer.InstallAsync(_paths, _options, CancellationToken.None);
        changed.Should().BeTrue();

        var content = await File.ReadAllTextAsync(manifestPath);
        content.Should().NotContain("tampered");
        content.Should().Contain("com.wtdeck.streamdock.gear");
    }
}
