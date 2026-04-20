using FluentAssertions;
using WTDeck.Core.Audio;

namespace WTDeck.Core.Tests.Audio;

public class ToneGeneratorTests
{
    [Fact]
    public void Correct_sample_count_for_duration()
    {
        var samples = ToneGenerator.GenerateSamples(440f, 44100, 1000);
        samples.Should().HaveCount(44100);
    }

    [Fact]
    public void Short_duration_produces_correct_count()
    {
        var samples = ToneGenerator.GenerateSamples(440f, 44100, 250);
        samples.Should().HaveCount(11025);
    }

    [Fact]
    public void All_samples_within_valid_range()
    {
        var samples = ToneGenerator.GenerateSamples(800f, 44100, 100);
        samples.Should().AllSatisfy(s =>
        {
            s.Should().BeGreaterOrEqualTo(-1.0f);
            s.Should().BeLessOrEqualTo(1.0f);
        });
    }

    [Fact]
    public void Zero_frequency_produces_silence()
    {
        var samples = ToneGenerator.GenerateSamples(0f, 44100, 100);
        samples.Should().AllSatisfy(s => s.Should().Be(0f));
    }

    [Fact]
    public void BeepPulse_has_correct_total_length()
    {
        // 500ms beep + 500ms silence @ 44100 Hz = 44100 samples total
        var pulse = ToneGenerator.GenerateBeepPulse(800f, 44100, beepMs: 500, silenceMs: 500);
        pulse.Should().HaveCount(44100);
    }

    [Fact]
    public void BeepPulse_silence_tail_is_zero()
    {
        var pulse = ToneGenerator.GenerateBeepPulse(800f, 44100, beepMs: 500, silenceMs: 500);
        var silenceStart = 22050; // 500 ms of samples
        for (var i = silenceStart; i < pulse.Length; i++)
            pulse[i].Should().Be(0f, $"sample {i} is in the silence portion");
    }

    [Fact]
    public void BeepPulse_starts_and_ends_at_zero_for_seamless_loop()
    {
        // With fade envelope, the first and last samples of the beep must be 0
        // so the loop wrap (end of silence -> start of next beep) is click-free.
        var pulse = ToneGenerator.GenerateBeepPulse(800f, 44100, beepMs: 500, silenceMs: 500);
        pulse[0].Should().Be(0f, "first sample must be zero for clean loop start");
        pulse[^1].Should().Be(0f, "last sample (in silence) must be zero");
    }

    [Fact]
    public void BeepPulse_applies_fade_in_envelope()
    {
        var pulse = ToneGenerator.GenerateBeepPulse(800f, 44100, beepMs: 500, silenceMs: 500, fadeMs: 5);
        var fadeCount = 44100 * 5 / 1000; // 5 ms at 44100 Hz = 220 samples

        // The peak sample during the fade-in region should be strictly smaller
        // than the peak of a fully-faded-in beep at normal amplitude.
        var maxInFade = 0f;
        for (var i = 0; i < fadeCount; i++)
            maxInFade = Math.Max(maxInFade, Math.Abs(pulse[i]));

        // Linear fade-in: max amplitude should be at most ~1.0 x (fadeCount - 1)/fadeCount ~= 0.995
        maxInFade.Should().BeLessThan(1.0f);
    }

    [Fact]
    public void BeepPulse_with_zero_beep_is_all_silence()
    {
        var pulse = ToneGenerator.GenerateBeepPulse(800f, 44100, beepMs: 0, silenceMs: 100);
        pulse.Should().HaveCount(4410);
        pulse.Should().AllSatisfy(s => s.Should().Be(0f));
    }
}
