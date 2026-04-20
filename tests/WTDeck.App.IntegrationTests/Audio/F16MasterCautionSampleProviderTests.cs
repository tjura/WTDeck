using FluentAssertions;
using WTDeck.App.Audio;

namespace WTDeck.App.IntegrationTests.Audio;

public class F16MasterCautionSampleProviderTests
{
    private const int SampleRate = 44100;

    [Fact]
    public void SamplesPerCycle_is_4_tones_times_83ms()
    {
        var provider = new F16MasterCautionSampleProvider(SampleRate, 0.5f);
        // 4 x (44100 x 83 / 1000) = 4 x 3660 = 14640
        provider.SamplesPerCycle.Should().Be(14640);
    }

    [Fact]
    public void Read_fills_requested_sample_count()
    {
        var provider = new F16MasterCautionSampleProvider(SampleRate, 0.5f);
        var buffer = new float[1024];

        var written = provider.Read(buffer, 0, buffer.Length);

        written.Should().Be(1024);
    }

    [Fact]
    public void All_samples_are_within_volume_envelope()
    {
        var provider = new F16MasterCautionSampleProvider(SampleRate, 0.5f);
        var buffer = new float[SampleRate]; // 1 second

        provider.Read(buffer, 0, buffer.Length);

        foreach (var sample in buffer)
        {
            sample.Should().BeGreaterOrEqualTo(-0.5f);
            sample.Should().BeLessOrEqualTo(0.5f);
        }
    }

    [Fact]
    public void Consecutive_samples_never_jump_by_more_than_a_small_delta()
    {
        // Key property of a click-free rendered loop: adjacent samples must
        // never show a huge amplitude step. A click is exactly that - a sudden
        // jump. Any jump larger than 0.1 would indicate a broken envelope or
        // loop boundary.
        var provider = new F16MasterCautionSampleProvider(SampleRate, 0.5f);
        var buffer = new float[SampleRate * 2]; // 2 full seconds, covers many cycles

        provider.Read(buffer, 0, buffer.Length);

        var maxJump = 0f;
        for (var i = 1; i < buffer.Length; i++)
            maxJump = Math.Max(maxJump, Math.Abs(buffer[i] - buffer[i - 1]));

        maxJump.Should().BeLessThan(0.1f,
            "a rendered alert loop should never produce a click");
    }

    [Fact]
    public void Read_across_multiple_calls_stays_continuous()
    {
        // Verify that splitting a Read across two calls produces the same
        // continuity as a single Read. If the provider had any hidden state or
        // bad wrap handling, there would be a discontinuity at the join.
        var provider = new F16MasterCautionSampleProvider(SampleRate, 0.5f);
        var buffer = new float[SampleRate];

        provider.Read(buffer, 0, 10_000);
        provider.Read(buffer, 10_000, buffer.Length - 10_000);

        var jumpAtBoundary = Math.Abs(buffer[10_000] - buffer[9_999]);
        jumpAtBoundary.Should().BeLessThan(0.1f);
    }

    [Fact]
    public void Cycle_wrap_back_to_first_tone_is_seamless()
    {
        // The loop wrap must be click-free.
        var provider = new F16MasterCautionSampleProvider(SampleRate, 0.5f);
        var cycleSize = provider.SamplesPerCycle;
        var buffer = new float[cycleSize * 2 + 100]; // two full cycles + a bit more

        provider.Read(buffer, 0, buffer.Length);

        var jumpAtWrap = Math.Abs(buffer[cycleSize] - buffer[cycleSize - 1]);
        jumpAtWrap.Should().BeLessThan(0.1f,
            "the cycle wrap (tone 4 -> tone 1) must stay click-free");
    }

    [Fact]
    public void Consecutive_cycles_are_sample_identical()
    {
        var provider = new F16MasterCautionSampleProvider(SampleRate, 0.5f);
        var cycleSize = provider.SamplesPerCycle;
        var buffer = new float[cycleSize * 2];

        provider.Read(buffer, 0, buffer.Length);

        for (var i = 0; i < cycleSize; i++)
            buffer[i + cycleSize].Should().Be(buffer[i], $"sample {i} should repeat exactly next cycle");
    }

    [Fact]
    public void Invalid_volume_throws()
    {
        var act = () => new F16MasterCautionSampleProvider(SampleRate, 1.5f);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
