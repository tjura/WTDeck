using NAudio.Wave;

namespace WTDeck.App.Audio;

/// <summary>
/// Loads an audio file into memory and loops only the detected non-silent
/// region, with a short boundary crossfade to hide mismatched edges.
/// </summary>
public sealed class LoopedAudioFileSampleProvider : ISampleProvider
{
    private const float DefaultSilenceThreshold = 0.01f;
    private const int DefaultCrossfadeMs = 5;

    private readonly float[] _samples;
    private readonly int _channels;
    private readonly int _loopStartFrame;
    private readonly int _loopLengthFrames;
    private readonly int _crossfadeFrames;
    private int _framePosition;
    private int _channelPosition;

    public WaveFormat WaveFormat { get; }

    public int LoopStartFrame => _loopStartFrame;

    public int LoopEndFrameExclusive => _loopStartFrame + _loopLengthFrames;

    public LoopedAudioFileSampleProvider(
        float[] samples,
        int sampleRate,
        int channels,
        float silenceThreshold = DefaultSilenceThreshold,
        int crossfadeMs = DefaultCrossfadeMs)
    {
        if (samples is null) throw new ArgumentNullException(nameof(samples));
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
        if (samples.Length % channels != 0) throw new ArgumentException("Sample count must align to frame boundaries.", nameof(samples));
        if (silenceThreshold < 0f) throw new ArgumentOutOfRangeException(nameof(silenceThreshold));
        if (crossfadeMs < 0) throw new ArgumentOutOfRangeException(nameof(crossfadeMs));

        _samples = samples;
        _channels = channels;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

        var totalFrames = samples.Length / channels;
        (_loopStartFrame, var loopEndExclusive) = FindLoopRegion(samples, channels, silenceThreshold);
        _loopLengthFrames = Math.Max(1, loopEndExclusive - _loopStartFrame);

        var requestedCrossfadeFrames = sampleRate * crossfadeMs / 1000;
        _crossfadeFrames = Math.Min(requestedCrossfadeFrames, Math.Max(0, (_loopLengthFrames / 2) - 1));
    }

    public static LoopedAudioFileSampleProvider FromFile(
        string filePath,
        float silenceThreshold = DefaultSilenceThreshold,
        int crossfadeMs = DefaultCrossfadeMs)
    {
        using var reader = new AudioFileReader(filePath);
        var samples = ReadAllSamples(reader);
        return new LoopedAudioFileSampleProvider(
            samples,
            reader.WaveFormat.SampleRate,
            reader.WaveFormat.Channels,
            silenceThreshold,
            crossfadeMs);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        for (var i = 0; i < count; i++)
        {
            buffer[offset + i] = GetSample(_framePosition, _channelPosition);
            AdvancePosition();
        }

        return count;
    }

    private float GetSample(int framePosition, int channel)
    {
        if (_crossfadeFrames > 0 && framePosition >= _loopLengthFrames - _crossfadeFrames)
        {
            var fadeFrame = framePosition - (_loopLengthFrames - _crossfadeFrames);
            var blend = (fadeFrame + 1f) / (_crossfadeFrames + 1f);
            var tail = ReadFrameSample(_loopStartFrame + framePosition, channel);
            var head = ReadFrameSample(_loopStartFrame + fadeFrame, channel);
            return (tail * (1f - blend)) + (head * blend);
        }

        return ReadFrameSample(_loopStartFrame + framePosition, channel);
    }

    private float ReadFrameSample(int frame, int channel)
        => _samples[(frame * _channels) + channel];

    private void AdvancePosition()
    {
        _channelPosition++;
        if (_channelPosition < _channels)
            return;

        _channelPosition = 0;
        _framePosition++;
        if (_framePosition >= _loopLengthFrames)
            _framePosition = 0;
    }

    private static (int StartFrame, int EndFrameExclusive) FindLoopRegion(float[] samples, int channels, float silenceThreshold)
    {
        var totalFrames = samples.Length / channels;
        var start = 0;
        while (start < totalFrames && IsSilentFrame(samples, channels, start, silenceThreshold))
            start++;

        var end = totalFrames;
        while (end > start && IsSilentFrame(samples, channels, end - 1, silenceThreshold))
            end--;

        return start < end ? (start, end) : (0, totalFrames);
    }

    private static bool IsSilentFrame(float[] samples, int channels, int frame, float threshold)
    {
        var sampleOffset = frame * channels;
        for (var channel = 0; channel < channels; channel++)
        {
            if (MathF.Abs(samples[sampleOffset + channel]) > threshold)
                return false;
        }

        return true;
    }

    private static float[] ReadAllSamples(AudioFileReader reader)
    {
        var result = new List<float>(checked((int)Math.Min(reader.Length / sizeof(float), int.MaxValue)));
        var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];

        while (true)
        {
            var read = reader.Read(buffer, 0, buffer.Length);
            if (read == 0)
                break;

            for (var i = 0; i < read; i++)
                result.Add(buffer[i]);
        }

        return result.ToArray();
    }
}
