using NAudio.Wave;
using WTDeck.Core.Audio;

namespace WTDeck.App.Audio;

/// <summary>
/// Produces the F-16 AN/ALR-67 Master Caution tone as a deterministically
/// repeating NAudio sample source.
///
/// The real warning is a four-tone descending chirp: 550 Hz -> 520 Hz -> 490 Hz
/// -> 470 Hz, each tone 83 ms long (total sequence 332 ms). This implementation
/// loops the sequence indefinitely while <see cref="SyntheticSoundAlert"/> is
/// playing it.
///
/// The earlier implementation used a free-running phase accumulator and only
/// repeated the frequency schedule. That was phase-continuous, but it did not
/// actually replay the same alarm cycle each time, so the pattern drifted.
///
/// This version pre-renders one full cycle with short fades at each tone edge,
/// then loops that immutable buffer. The first and last samples of the cycle are
/// zero, every cycle is sample-identical to the previous one, and the loop wrap
/// is deterministic.
/// </summary>
public sealed class F16MasterCautionSampleProvider : ISampleProvider
{
    // Spec: AN/ALR-67 Master Caution is a 4-tone descending chirp.
    private static readonly float[] Frequencies = [550f, 520f, 490f, 470f];
    private const int ToneDurationMs = 83;
    private const int ToneFadeMs = 5;

    public WaveFormat WaveFormat { get; }

    private readonly float[] _cycleSamples;
    private long _sampleIndex;

    public F16MasterCautionSampleProvider(int sampleRate, float volume)
    {
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (volume is < 0f or > 1f) throw new ArgumentOutOfRangeException(nameof(volume));

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        _cycleSamples = BuildCycle(sampleRate, volume);
    }

    /// <summary>Number of samples in one full four-tone sequence (for tests).</summary>
    public int SamplesPerCycle => _cycleSamples.Length;

    public int Read(float[] buffer, int offset, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var cycleIndex = (int)(_sampleIndex % _cycleSamples.Length);
            buffer[offset + i] = _cycleSamples[cycleIndex];
            _sampleIndex++;
        }

        return count;
    }

    private static float[] BuildCycle(int sampleRate, float volume)
    {
        var samplesPerTone = sampleRate * ToneDurationMs / 1000;
        var cycle = new float[samplesPerTone * Frequencies.Length];
        var fadeCount = sampleRate * ToneFadeMs / 1000;

        for (var toneIndex = 0; toneIndex < Frequencies.Length; toneIndex++)
        {
            var tone = ToneGenerator.GenerateSamples(Frequencies[toneIndex], sampleRate, ToneDurationMs);
            ApplyFadeInOut(tone, fadeCount);

            for (var i = 0; i < tone.Length; i++)
                cycle[(toneIndex * samplesPerTone) + i] = tone[i] * volume;
        }

        return cycle;
    }

    private static void ApplyFadeInOut(float[] samples, int fadeCount)
    {
        if (samples.Length == 0 || fadeCount <= 0)
            return;

        if (fadeCount * 2 > samples.Length)
            fadeCount = samples.Length / 2;

        for (var i = 0; i < fadeCount; i++)
        {
            var factor = (float)i / fadeCount;
            samples[i] *= factor;
            samples[samples.Length - 1 - i] *= factor;
        }
    }
}
