using FluentAssertions;
using WTDeck.App.Audio;

namespace WTDeck.App.IntegrationTests.Audio;

public class LoopedAudioFileSampleProviderTests
{
    [Fact]
    public void Trims_silent_edges_from_loop_region()
    {
        var samples = new float[]
        {
            0f, 0f,
            0.001f, 0.001f,
            0.5f, 0.5f,
            0.25f, 0.25f,
            0f, 0f,
            0f, 0f,
        };

        var provider = new LoopedAudioFileSampleProvider(samples, 44100, 2, silenceThreshold: 0.01f, crossfadeMs: 0);

        provider.LoopStartFrame.Should().Be(2);
        provider.LoopEndFrameExclusive.Should().Be(4);
    }

    [Fact]
    public void Loops_trimmed_region_without_reintroducing_edge_silence()
    {
        var samples = new float[]
        {
            0f,
            0f,
            0.75f,
            0.25f,
            0f,
        };

        var provider = new LoopedAudioFileSampleProvider(samples, 44100, 1, silenceThreshold: 0.01f, crossfadeMs: 0);
        var buffer = new float[6];

        provider.Read(buffer, 0, buffer.Length);

        buffer.Should().Equal([0.75f, 0.25f, 0.75f, 0.25f, 0.75f, 0.25f]);
    }

    [Fact]
    public void Crossfades_boundary_when_requested()
    {
        var samples = new float[]
        {
            1f,
            1f,
            -1f,
            -1f,
        };

        var provider = new LoopedAudioFileSampleProvider(samples, 1000, 1, silenceThreshold: 0f, crossfadeMs: 1);
        var buffer = new float[4];

        provider.Read(buffer, 0, buffer.Length);

        buffer[0].Should().Be(1f);
        buffer[1].Should().Be(1f);
        buffer[2].Should().Be(-1f);
        buffer[3].Should().Be(0f);
    }
}
