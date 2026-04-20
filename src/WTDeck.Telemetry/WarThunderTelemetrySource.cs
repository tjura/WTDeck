using System.Text.Json;
using Microsoft.Extensions.Logging;
using WTDeck.Core.Interfaces;
using WTDeck.Core.Models;

namespace WTDeck.Telemetry;

/// <summary>
/// War Thunder telemetry source.
///
/// Fetches both /indicators (for aircraft type + handle command) and /state
/// (for the actual gear position as a percentage). The two endpoints report
/// different aspects of the same physical subsystem:
///   - /indicators."gears" reflects the gear-handle command (0 or 1).
///   - /state."gear, %" reflects the actual gear-extension percentage.
/// The rule engine needs the actual position to show mid-transit states,
/// so /state is the authoritative source for gear position.
/// </summary>
public sealed class WarThunderTelemetrySource : ITelemetrySource
{
    private readonly HttpClient _httpClient;
    private readonly string _indicatorsUrl;
    private readonly string _stateUrl;
    private readonly ILogger<WarThunderTelemetrySource> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    public bool IsAvailable { get; private set; }

    public WarThunderTelemetrySource(
        HttpClient httpClient,
        TelemetryOptions options,
        ILogger<WarThunderTelemetrySource> logger)
    {
        _httpClient = httpClient;
        var baseUrl = options.BaseUrl.TrimEnd('/');
        _indicatorsUrl = $"{baseUrl}/indicators";
        _stateUrl = $"{baseUrl}/state";
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromMilliseconds(options.HttpTimeoutMs);
    }

    public async Task<FlightSnapshot?> GetCurrentStateAsync(CancellationToken ct)
    {
        try
        {
            var indicatorsTask = FetchAsync<IndicatorsResponse>(_indicatorsUrl, ct);
            var stateTask = FetchAsync<StateResponse>(_stateUrl, ct);

            await Task.WhenAll(indicatorsTask, stateTask);

            var indicators = indicatorsTask.Result;
            var state = stateTask.Result;

            if (indicators is null && state is null)
            {
                IsAvailable = false;
                return null;
            }

            IsAvailable = true;
            return TelemetryMapper.ToSnapshot(indicators, state);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Telemetry fetch failed");
            IsAvailable = false;
            return null;
        }
    }

    private async Task<T?> FetchAsync<T>(string url, CancellationToken ct) where T : class
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
