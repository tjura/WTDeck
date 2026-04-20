using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using WTDeck.Core.Interfaces;
using WTDeck.Core.Models;

namespace WTDeck.Telemetry;

public sealed class ScenarioTelemetrySource : ITelemetrySource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private readonly TelemetryScenarioFile _scenario;
    private readonly ILogger<ScenarioTelemetrySource> _logger;
    private readonly Channel<TelemetryScenarioExecution> _executions = Channel.CreateUnbounded<TelemetryScenarioExecution>();
    private int _nextStepIndex;
    private FlightSnapshot? _lastSnapshot;
    private bool _completed;

    public ScenarioTelemetrySource(TelemetryScenarioFile scenario, ILogger<ScenarioTelemetrySource> logger)
    {
        _scenario = scenario;
        _logger = logger;
    }

    public TelemetryScenarioFile Scenario => _scenario;
    public bool IsAvailable { get; private set; }

    public async Task<FlightSnapshot?> GetCurrentStateAsync(CancellationToken ct)
    {
        if (_nextStepIndex >= _scenario.Steps.Count)
        {
            CompleteOnce();
            return _lastSnapshot;
        }

        var step = _scenario.Steps[_nextStepIndex];
        var indicators = Deserialize<IndicatorsResponse>(step.IndicatorsJson);
        var state = Deserialize<StateResponse>(step.StateJson);
        var snapshot = indicators is null && state is null
            ? null
            : TelemetryMapper.ToSnapshot(indicators, state);

        _lastSnapshot = snapshot;
        IsAvailable = snapshot is not null;

        var stepNumber = _nextStepIndex + 1;
        _nextStepIndex++;

        _logger.LogInformation("Scenario step {Step}/{Total}: {Name}", stepNumber, _scenario.Steps.Count, step.Name);

        await _executions.Writer.WriteAsync(new TelemetryScenarioExecution(stepNumber, step, snapshot, IsAvailable), ct);

        if (_nextStepIndex >= _scenario.Steps.Count)
            CompleteOnce();

        return snapshot;
    }

    public IAsyncEnumerable<TelemetryScenarioExecution> ReadExecutionsAsync(CancellationToken ct)
        => _executions.Reader.ReadAllAsync(ct);

    private static T? Deserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private void CompleteOnce()
    {
        if (_completed)
            return;

        _completed = true;
        _executions.Writer.TryComplete();
    }
}

public sealed record TelemetryScenarioExecution(
    int StepNumber,
    TelemetryScenarioStep Step,
    FlightSnapshot? Snapshot,
    bool IsAvailable);
