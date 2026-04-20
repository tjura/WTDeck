using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WTDeck.StreamDock.Configuration;

namespace WTDeck.StreamDock.Profiles;

public sealed class ProfileInstaller
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ILogger<ProfileInstaller> _logger;
    private readonly ProfileManifestBuilder _builder;

    public ProfileInstaller(ILogger<ProfileInstaller> logger, ProfileManifestBuilder builder)
    {
        _logger = logger;
        _builder = builder;
    }

    /// <summary>
    /// Installs or updates the WTDeck profile.
    /// Returns true if the profile changed.
    /// </summary>
    public async Task<bool> EnsureProfileAsync(
        StreamDockPaths paths,
        StreamDockOptions options,
        CancellationToken ct)
    {
        if (!Directory.Exists(paths.ProfilesDir))
        {
            _logger.LogWarning("StreamDock profiles directory does not exist: {Path}", paths.ProfilesDir);
            return false;
        }

        var (profileUuid, _, manifest) = _builder.Build(options);
        var profileDirName = $"{profileUuid}.sdProfile";
        var profileDir = Path.Combine(paths.ProfilesDir, profileDirName);
        Directory.CreateDirectory(profileDir);

        var manifestPath = Path.Combine(profileDir, "manifest.json");
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

        if (File.Exists(manifestPath) && !options.ForceOverwriteProfile && !FileContentEquals(manifestPath, jsonBytes))
        {
            _logger.LogWarning(
                "Profile manifest differs at {Path} and ForceOverwriteProfile is false - skipping",
                manifestPath);
            return false;
        }

        if (File.Exists(manifestPath) && FileContentEquals(manifestPath, jsonBytes))
        {
            _logger.LogInformation("Profile up-to-date at {Path}", profileDir);
            return false;
        }

        // Atomic write: write to temp file, then replace
        var tempPath = manifestPath + ".tmp";
        await File.WriteAllBytesAsync(tempPath, jsonBytes, ct);

        if (File.Exists(manifestPath))
            File.Delete(manifestPath);
        File.Move(tempPath, manifestPath);

        _logger.LogInformation("Profile installed at {Path}", profileDir);
        return true;
    }

    private static bool FileContentEquals(string path, byte[] expected)
    {
        try
        {
            var existing = File.ReadAllBytes(path);
            if (existing.Length != expected.Length) return false;
            return SHA256.HashData(existing).AsSpan().SequenceEqual(SHA256.HashData(expected));
        }
        catch
        {
            return false;
        }
    }
}
