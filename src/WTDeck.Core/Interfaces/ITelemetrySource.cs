using WTDeck.Core.Models;

namespace WTDeck.Core.Interfaces;

public interface ITelemetrySource
{
    Task<FlightSnapshot?> GetCurrentStateAsync(CancellationToken ct);
    bool IsAvailable { get; }
}
