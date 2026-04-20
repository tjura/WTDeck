namespace WTDeck.StreamDock.Configuration;

public sealed class StreamDockPaths
{
    public string UserDataRoot { get; }
    public string PluginsDir { get; }
    public string ProfilesDir { get; }
    public string? InstallDir { get; }
    public string? ExecutablePath { get; }

    public StreamDockPaths(StreamDockOptions options)
    {
        UserDataRoot = options.UserDataRoot
                       ?? Path.Combine(
                           Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                           "HotSpot",
                           "StreamDock");

        PluginsDir = Path.Combine(UserDataRoot, "plugins");
        ProfilesDir = Path.Combine(UserDataRoot, "profiles");

        InstallDir = options.InstallDir ?? DetectInstallDir();
        ExecutablePath = InstallDir is not null
            ? Path.Combine(InstallDir, "Stream Controller.exe")
            : null;
    }

    public bool IsInstalled =>
        Directory.Exists(UserDataRoot)
        && ExecutablePath is not null
        && File.Exists(ExecutablePath);

    private static string? DetectInstallDir()
    {
        string[] candidates =
        [
            @"C:\Program Files (x86)\Stream Controller",
            @"C:\Program Files\Stream Controller",
        ];

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "Stream Controller.exe")))
                return candidate;
        }

        return null;
    }
}
