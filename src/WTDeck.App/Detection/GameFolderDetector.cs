using Microsoft.Win32;

namespace WTDeck.App.Detection;

public static class GameFolderDetector
{
    private static readonly string[] CommonPaths =
    [
        @"C:\Program Files (x86)\Steam\steamapps\common\War Thunder",
        @"C:\Program Files\Steam\steamapps\common\War Thunder",
        @"D:\SteamLibrary\steamapps\common\War Thunder",
        @"D:\Steam\steamapps\common\War Thunder",
        @"E:\SteamLibrary\steamapps\common\War Thunder",
    ];

    public static string? Detect()
    {
        // Try Steam registry first
        var steamPath = TryFindViaSteam();
        if (steamPath is not null)
            return steamPath;

        // Fallback to common paths
        foreach (var path in CommonPaths)
        {
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "aces.exe")))
                return path;
        }

        return null;
    }

    private static string? TryFindViaSteam()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            var steamRoot = key?.GetValue("InstallPath") as string;
            if (steamRoot is null)
                return null;

            // Check default library
            var defaultPath = Path.Combine(steamRoot, "steamapps", "common", "War Thunder");
            if (Directory.Exists(defaultPath))
                return defaultPath;

            // Parse libraryfolders.vdf for additional libraries
            var vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdfPath))
                return null;

            var lines = File.ReadAllLines(vdfPath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = trimmed.Split('"');
                if (parts.Length < 4) continue;

                var libPath = parts[3].Replace("\\\\", "\\");
                var wtPath = Path.Combine(libPath, "steamapps", "common", "War Thunder");
                if (Directory.Exists(wtPath))
                    return wtPath;
            }
        }
        catch
        {
            // Registry access may fail - not critical
        }

        return null;
    }
}
