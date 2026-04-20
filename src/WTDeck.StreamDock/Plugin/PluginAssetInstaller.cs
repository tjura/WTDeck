using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using WTDeck.StreamDock.Configuration;

namespace WTDeck.StreamDock.Plugin;

public sealed class PluginAssetInstaller
{
    private readonly ILogger<PluginAssetInstaller> _logger;
    private readonly Assembly _resourceAssembly;

    public PluginAssetInstaller(ILogger<PluginAssetInstaller> logger, Assembly? resourceAssembly = null)
    {
        _logger = logger;
        _resourceAssembly = resourceAssembly ?? typeof(PluginAssetManifest).Assembly;
    }

    /// <summary>
    /// Installs the plugin to {PluginsDir}\{pluginUuid}.sdPlugin\.
    /// Returns true if any file was written or overwritten.
    /// </summary>
    public async Task<bool> InstallAsync(
        StreamDockPaths paths,
        StreamDockOptions options,
        CancellationToken ct)
    {
        if (!Directory.Exists(paths.PluginsDir))
        {
            _logger.LogWarning("StreamDock plugins directory does not exist: {Path}", paths.PluginsDir);
            return false;
        }

        var pluginDir = Path.Combine(paths.PluginsDir, $"{options.PluginUuid}.sdPlugin");
        Directory.CreateDirectory(pluginDir);

        var changed = false;

        foreach (var (resourceName, relativePath) in PluginAssetManifest.ResourceMap)
        {
            await using var source = PluginAssetManifest.OpenResource(resourceName, _resourceAssembly);
            if (source is null)
            {
                _logger.LogWarning("Embedded resource not found: {Resource}", resourceName);
                continue;
            }

            var targetPath = Path.Combine(pluginDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir))
                Directory.CreateDirectory(targetDir);

            using var ms = new MemoryStream();
            await source.CopyToAsync(ms, ct);
            var sourceBytes = ms.ToArray();

            if (!File.Exists(targetPath) || !FilesAreEqual(targetPath, sourceBytes))
            {
                await File.WriteAllBytesAsync(targetPath, sourceBytes, ct);
                changed = true;
                _logger.LogDebug("Wrote plugin file: {Path}", targetPath);
            }
        }

        if (changed)
            _logger.LogInformation("Plugin installed/updated at {Path}", pluginDir);
        else
            _logger.LogInformation("Plugin up-to-date at {Path}", pluginDir);

        return changed;
    }

    private static bool FilesAreEqual(string path, byte[] expected)
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
