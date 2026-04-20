using FluentAssertions;
using WTDeck.App.Concurrency;

namespace WTDeck.App.IntegrationTests.Concurrency;

public class LatestValueSignalTests
{
    [Fact]
    public async Task Unread_values_are_coalesced_to_latest()
    {
        var signal = new LatestValueSignal<int>();
        signal.TryPost(1).Should().BeTrue();
        signal.TryPost(2).Should().BeTrue();
        signal.TryPost(3).Should().BeTrue();
        signal.Complete();

        var values = new List<int>();
        await foreach (var value in signal.ReadAllAsync(CancellationToken.None))
            values.Add(value);

        values.Should().Equal([3]);
    }

    [Fact]
    public async Task Values_continue_to_flow_after_reader_catches_up()
    {
        var signal = new LatestValueSignal<int>();
        await using var enumerator = signal.ReadAllAsync(CancellationToken.None).GetAsyncEnumerator();

        signal.TryPost(10).Should().BeTrue();
        (await enumerator.MoveNextAsync()).Should().BeTrue();
        enumerator.Current.Should().Be(10);

        signal.TryPost(20).Should().BeTrue();
        signal.Complete();

        (await enumerator.MoveNextAsync()).Should().BeTrue();
        enumerator.Current.Should().Be(20);
        (await enumerator.MoveNextAsync()).Should().BeFalse();
    }
}
