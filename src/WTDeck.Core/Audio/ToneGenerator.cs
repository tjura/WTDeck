namespace WTDeck.Core.Audio;

public static class ToneGenerator
{
    /// <summary>
    /// Generates a sine wave of the given frequency and duration.
    /// </summary>
    public static float[] GenerateSamples(float frequency, int sampleRate, int durationMs)
    {
        var sampleCount = (int)(sampleRate * durationMs / 1000.0);
        var samples = new float[sampleCount];

        if (frequency <= 0f)
            return samples;

        for (var i = 0; i < sampleCount; i++)
        {
            samples[i] = MathF.Sin(2 * MathF.PI * frequency * i / sampleRate);
        }

        return samples;
    }

    /// <summary>
    /// Generates a loopable "beep-silence" pulse suitable for cockpit-style alerts.
    ///
    /// Layout: [beep samples with fade-in/out envelope][silence samples].
    /// A short linear envelope is applied to the start and end of the beep so
    /// the pulse transitions smoothly to silence with no click, and the loop
    /// wrap (end of silence -> start of next beep) is seamless because both
    /// edges are exactly zero.
    /// </summary>
    public static float[] GenerateBeepPulse(
        float frequency,
        int sampleRate,
        int beepMs,
        int silenceMs,
        int fadeMs = 5)
    {
        if (beepMs < 0) throw new ArgumentOutOfRangeException(nameof(beepMs));
        if (silenceMs < 0) throw new ArgumentOutOfRangeException(nameof(silenceMs));
        if (fadeMs < 0) throw new ArgumentOutOfRangeException(nameof(fadeMs));

        var beepSamples = GenerateSamples(frequency, sampleRate, beepMs);
        var silenceCount = (int)(sampleRate * silenceMs / 1000.0);
        var fadeCount = (int)(sampleRate * fadeMs / 1000.0);

        // Clamp fade to at most half the beep so fade-in and fade-out don't overlap.
        if (fadeCount * 2 > beepSamples.Length)
            fadeCount = beepSamples.Length / 2;

        if (fadeCount > 0)
        {
            for (var i = 0; i < fadeCount; i++)
            {
                var factor = (float)i / fadeCount;
                beepSamples[i] *= factor;
                beepSamples[beepSamples.Length - 1 - i] *= factor;
            }
        }

        var result = new float[beepSamples.Length + silenceCount];
        Array.Copy(beepSamples, 0, result, 0, beepSamples.Length);
        // The silence tail is already zero-initialized.
        return result;
    }
}
