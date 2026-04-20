using NAudio.Wave;
using WTDeck.Core.Interfaces;

namespace WTDeck.App.Audio;

/// <summary>
/// Primary flight-assistant danger alarm: the F-16 AN/ALR-67 Master Caution
/// four-tone descending chirp (550 -> 520 -> 490 -> 470 Hz, 83 ms per tone),
/// looped continuously while at least one alert is <c>Active</c>.
///
/// The underlying <see cref="F16MasterCautionSampleProvider"/> renders one
/// fully-shaped chirp cycle and loops it deterministically, so every repeat is
/// sample-identical and the wrap point remains click-free.
/// </summary>
public sealed class SyntheticSoundAlert : ISoundAlert
{
    private const int SampleRate = 44100;
    private const float Volume = 0.5f;

    private WaveOutEvent? _waveOut;
    private volatile bool _isPlaying;
    private readonly object _lock = new();

    public void PlayDangerTone()
    {
        lock (_lock)
        {
            if (_isPlaying)
                return;

            _isPlaying = true;

            var provider = new F16MasterCautionSampleProvider(SampleRate, Volume);
            _waveOut = new WaveOutEvent
            {
                // Lower default latency for quicker stop response. NAudio still
                // double-buffers, which is fine because the provider is stateful
                // and has no discontinuities between buffer fills.
                DesiredLatency = 100,
                NumberOfBuffers = 3,
            };
            _waveOut.Init(provider);
            _waveOut.Play();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _isPlaying = false;
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;
        }
    }

    public void Dispose() => Stop();
}
