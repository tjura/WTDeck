namespace WTDeck.StreamDock.Interfaces;

public interface IPluginSyncService
{
    Task<SyncResult> EnsureInstalledAsync(CancellationToken ct);
}

public sealed record SyncResult(
    bool Synced,
    bool PluginChanged,
    bool ProfileChanged,
    bool Restarted,
    string? Warning = null);
