using Microsoft.Extensions.Logging;
using WTDeck.Core.Interfaces;
using WTDeck.Core.Models;

namespace WTDeck.Telemetry;

/// <summary>
/// Polls the War Thunder telemetry source on a fixed interval and fires
/// <see cref="StateChanged"/> every tick.
///
/// Unlike the earlier version, this service intentionally does NOT deduplicate
/// snapshots - rules need to evaluate every tick so they can raise and clear
/// alerts based on continuously-changing values like IAS, AoA, and G-load.
/// The downstream <c>AppHost</c> dedupes outbound IPC updates per-action.
/// </summary>
public sealed class TelemetryPollingService
{
    private readonly ITelemetrySource _source;
    private readonly TelemetryOptions _options;
    private readonly ILogger<TelemetryPollingService> _logger;

    public event EventHandler<FlightSnapshot?>? StateChanged;

    public TelemetryPollingService(
        ITelemetrySource source,
        TelemetryOptions options,
        ILogger<TelemetryPollingService> logger)
    {
        _source = source;
        _options = options;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("Telemetry polling started (interval: {Interval}ms)", _options.PollIntervalMs);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var snapshot = await _source.GetCurrentStateAsync(ct);
                StateChanged?.Invoke(this, snapshot);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Telemetry poll cycle failed");
            }

            await Task.Delay(_options.PollIntervalMs, ct);
        }

        _logger.LogInformation("Telemetry polling stopped");
    }
}
