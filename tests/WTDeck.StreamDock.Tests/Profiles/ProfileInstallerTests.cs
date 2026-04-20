using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WTDeck.StreamDock.Configuration;
using WTDeck.StreamDock.Profiles;

namespace WTDeck.StreamDock.Tests.Profiles;

public class ProfileInstallerTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly StreamDockPaths _paths;
    private readonly StreamDockOptions _options;
    private readonly ProfileInstaller _installer;

    public ProfileInstallerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wtdeck-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempRoot, "plugins"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "profiles"));

        _options = new StreamDockOptions
        {
            UserDataRoot = _tempRoot,
            InstallDir = null,
            ProfileName = "WTDeck-Test",
            DeviceUUID = "TestDevice",
            DeviceSerialNumber = "TEST123",
            DeviceModel = "TESTMODEL"
        };

        _paths = new StreamDockPaths(_options);
        _installer = new ProfileInstaller(
            NullLogger<ProfileInstaller>.Instance,
            new ProfileManifestBuilder());
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task First_install_writes_manifest_and_returns_true()
    {
        var changed = await _installer.EnsureProfileAsync(_paths, _options, CancellationToken.None);
        changed.Should().BeTrue();

        var profileDirs = Directory.GetDirectories(_paths.ProfilesDir, "*.sdProfile");
        profileDirs.Should().HaveCount(1);
        File.Exists(Path.Combine(profileDirs[0], "manifest.json")).Should().BeTrue();
    }

    [Fact]
    public async Task Re_install_with_same_content_is_no_op()
    {
        await _installer.EnsureProfileAsync(_paths, _options, CancellationToken.None);
        var second = await _installer.EnsureProfileAsync(_paths, _options, CancellationToken.None);

        second.Should().BeFalse();
    }

    [Fact]
    public async Task Re_install_with_different_options_overwrites_when_forced()
    {
        await _installer.EnsureProfileAsync(_paths, _options, CancellationToken.None);

        _options.DeviceSerialNumber = "CHANGED";
        _options.ForceOverwriteProfile = true;
        var second = await _installer.EnsureProfileAsync(_paths, _options, CancellationToken.None);

        second.Should().BeTrue();
    }
}
