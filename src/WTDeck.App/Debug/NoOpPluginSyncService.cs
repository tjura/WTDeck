using WTDeck.StreamDock.Interfaces;

namespace WTDeck.App.Debug;

public sealed class NoOpPluginSyncService : IPluginSyncService
{
    public Task<SyncResult> EnsureInstalledAsync(CancellationToken ct)
        => Task.FromResult(new SyncResult(false, false, false, false));
}
