namespace WTDeck.Core.KeyBindings;

public static class BlkFileDetector
{
    public static string? FindBestBlkFile(string? savesDirectory = null)
    {
        var dir = savesDirectory ?? GetDefaultSavesDirectory();
        if (dir is null || !Directory.Exists(dir))
            return null;

        var blkFiles = EnumerateBlkFiles(dir)
            .Where(IsKeyBindingFile)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        if (blkFiles.Count > 0)
            return blkFiles[0];

        // Fallback: look for LAST-CONTROLER.blk specifically
        var fallback = Path.Combine(dir, "LAST-CONTROLER.blk");
        return File.Exists(fallback) ? fallback : null;
    }

    private static IEnumerable<string> EnumerateBlkFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*.blk", SearchOption.AllDirectories);
        }
        catch
        {
            return [];
        }
    }

    public static string? GetDefaultSavesDirectory()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrEmpty(docs))
            return null;

        var path = Path.Combine(docs, "My Games", "WarThunder", "Saves");
        return Directory.Exists(path) ? path : null;
    }

    private static bool IsKeyBindingFile(string filePath)
    {
        try
        {
            // Read first 4KB to check for controls/hotkeys blocks
            using var reader = new StreamReader(filePath);
            var buffer = new char[4096];
            var read = reader.Read(buffer, 0, buffer.Length);
            var content = new string(buffer, 0, read);

            return content.Contains("controls{", StringComparison.OrdinalIgnoreCase)
                   || content.Contains("controls {", StringComparison.OrdinalIgnoreCase)
                   || content.Contains("hotkeys{", StringComparison.OrdinalIgnoreCase)
                   || content.Contains("hotkeys {", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
