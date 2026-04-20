using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using WTDeck.App.Configuration;
using WTDeck.Core.Interfaces;

namespace WTDeck.App.Audio;

/// <summary>
/// Plays the configured primary alert WAV in a loop, with a synthetic fallback
/// when the file is unavailable.
/// </summary>
public sealed class PrimarySoundAlert : ISoundAlert
{
    private const int SampleRate = 44100;
    private const float SyntheticVolume = 0.5f;

    private readonly AppOptions _options;
    private readonly ILogger<PrimarySoundAlert> _logger;
    private readonly object _lock = new();

    private WaveOutEvent? _waveOut;
    private IDisposable? _playbackLifetime;
    private volatile bool _isPlaying;

    public PrimarySoundAlert(AppOptions options, ILogger<PrimarySoundAlert> logger)
    {
        _options = options;
        _logger = logger;
    }

    public void PlayDangerTone()
    {
        lock (_lock)
        {
            if (_isPlaying)
                return;

            try
            {
                var playback = CreatePlayback();
                var waveOut = new WaveOutEvent
                {
                    DesiredLatency = 100,
                    NumberOfBuffers = 3,
                };

                waveOut.Init(playback.Provider);
                waveOut.Play();

                _waveOut = waveOut;
                _playbackLifetime = playback.Lifetime;
                _isPlaying = true;
                _logger.LogInformation("Danger alert sound started using {Source}", playback.Description);
            }
            catch (Exception ex)
            {
                DisposePlaybackNoLock();
                _isPlaying = false;
                _logger.LogWarning(ex, "Failed to start danger alert sound");
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _isPlaying = false;
            DisposePlaybackNoLock();
        }
    }

    public void Dispose() => Stop();

    private PlaybackSession CreatePlayback()
    {
        var filePath = ResolveAlertFilePath(_options.PrimaryAlertFile);
        if (filePath is not null)
        {
            try
            {
                var loopedProvider = LoopedAudioFileSampleProvider.FromFile(filePath);
                return new PlaybackSession(loopedProvider.ToWaveProvider(), null, $"wave file '{filePath}'");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to open primary alert file '{FilePath}'. Falling back to synthetic alarm.",
                    filePath);
            }
        }

        if (!string.IsNullOrWhiteSpace(_options.PrimaryAlertFile) && filePath is null)
        {
            _logger.LogWarning(
                "Primary alert file '{ConfiguredPath}' was not found under '{BaseDirectory}'. Falling back to synthetic alarm.",
                _options.PrimaryAlertFile,
                AppContext.BaseDirectory);
        }

        var provider = new F16MasterCautionSampleProvider(SampleRate, SyntheticVolume);
        return new PlaybackSession(provider.ToWaveProvider(), null, "synthetic fallback");
    }

    private void DisposePlaybackNoLock()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;

        _playbackLifetime?.Dispose();
        _playbackLifetime = null;
    }

    private static string? ResolveAlertFilePath(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return null;

        if (Path.IsPathRooted(configuredPath))
            return File.Exists(configuredPath) ? configuredPath : null;

        var candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
        return File.Exists(candidate) ? candidate : null;
    }

    private sealed record PlaybackSession(IWaveProvider Provider, IDisposable? Lifetime, string Description);
}
