using System.Threading.Channels;

namespace WTDeck.App.Concurrency;

/// <summary>
/// Bounded async signal that retains only the latest unread value.
/// </summary>
public sealed class LatestValueSignal<T>
{
    private readonly Channel<T> _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropOldest,
    });

    public bool TryPost(T value) => _channel.Writer.TryWrite(value);

    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);

    public void Complete() => _channel.Writer.TryComplete();
}
