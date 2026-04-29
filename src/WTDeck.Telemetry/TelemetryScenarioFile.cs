using System.Text.Json;

namespace WTDeck.Telemetry;

public sealed class TelemetryScenarioFile
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public string Name { get; init; } = "scenario";
    public int StepIntervalMs { get; init; } = 100;
    public IReadOnlyList<TelemetryScenarioStep> Steps { get; init; } = [];

    public static TelemetryScenarioFile LoadFromFile(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Scenario file not found.", fullPath);

        using var document = JsonDocument.Parse(File.ReadAllText(fullPath));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Scenario root must be a JSON object.");

        var name = root.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
            ? nameElement.GetString() ?? "scenario"
            : Path.GetFileNameWithoutExtension(fullPath);

        var stepIntervalMs = root.TryGetProperty("stepIntervalMs", out var intervalElement) && intervalElement.TryGetInt32(out var parsedInterval)
            ? parsedInterval
            : 100;

        if (stepIntervalMs <= 0)
            throw new InvalidOperationException("stepIntervalMs must be greater than zero.");

        if (!root.TryGetProperty("steps", out var stepsElement) || stepsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Scenario must contain a 'steps' array.");

        var steps = new List<TelemetryScenarioStep>();
        var index = 0;
        foreach (var stepElement in stepsElement.EnumerateArray())
        {
            index++;
            var stepName = stepElement.TryGetProperty("name", out var stepNameElement) && stepNameElement.ValueKind == JsonValueKind.String
                ? stepNameElement.GetString() ?? $"step-{index}"
                : $"step-{index}";

            var indicatorsJson = stepElement.TryGetProperty("indicators", out var indicatorsElement)
                ? indicatorsElement.GetRawText()
                : null;

            var stateJson = stepElement.TryGetProperty("state", out var stateElement)
                ? stateElement.GetRawText()
                : null;

            var expectTelemetry = stepElement.TryGetProperty("expectTelemetry", out var telemetryExpectationElement)
                ? JsonSerializer.Deserialize<TelemetryScenarioTelemetryExpectation>(telemetryExpectationElement.GetRawText(), JsonOptions)
                : null;

            var expectUi = stepElement.TryGetProperty("expectUi", out var uiExpectationElement)
                ? JsonSerializer.Deserialize<TelemetryScenarioUiExpectation>(uiExpectationElement.GetRawText(), JsonOptions)
                : null;

            var expectPanel = stepElement.TryGetProperty("expectPanel", out var panelExpectationElement)
                ? JsonSerializer.Deserialize<TelemetryScenarioPanelExpectation>(panelExpectationElement.GetRawText(), JsonOptions)
                : null;

            var commands = stepElement.TryGetProperty("commands", out var commandElement)
                ? JsonSerializer.Deserialize<List<TelemetryScenarioCommand>>(commandElement.GetRawText(), JsonOptions) ?? []
                : [];

            steps.Add(new TelemetryScenarioStep
            {
                Name = stepName,
                IndicatorsJson = indicatorsJson,
                StateJson = stateJson,
                ExpectTelemetry = expectTelemetry,
                ExpectUi = expectUi,
                ExpectPanel = expectPanel,
                Commands = commands,
            });
        }

        if (steps.Count == 0)
            throw new InvalidOperationException("Scenario must contain at least one step.");

        return new TelemetryScenarioFile
        {
            Name = name,
            StepIntervalMs = stepIntervalMs,
            Steps = steps.AsReadOnly(),
        };
    }
}

public sealed class TelemetryScenarioStep
{
    public string Name { get; init; } = "step";
    public string? IndicatorsJson { get; init; }
    public string? StateJson { get; init; }
    public TelemetryScenarioTelemetryExpectation? ExpectTelemetry { get; init; }
    public TelemetryScenarioUiExpectation? ExpectUi { get; init; }
    public TelemetryScenarioPanelExpectation? ExpectPanel { get; init; }
    public IReadOnlyList<TelemetryScenarioCommand> Commands { get; init; } = [];
}

public sealed class TelemetryScenarioTelemetryExpectation
{
    public bool? Available { get; init; }
    public bool? Valid { get; init; }
    public string? AircraftType { get; init; }
    public float? GearPercent { get; init; }
    public float? Gear { get; init; }
    public float? GearsCommand { get; init; }
    public float? GearsLamp { get; init; }
    public float? IndicatedAirspeedKmh { get; init; }
    public float? LoadFactorNy { get; init; }
    public int? FlaresRemaining { get; init; }
}

public sealed class TelemetryScenarioUiExpectation
{
    public string ActionKey { get; init; } = "landing-gear";
    public string? Title { get; init; }
    public string? StatusKey { get; init; }
    public bool? IsBlinking { get; init; }
    public bool? IsEnabled { get; init; }
    public string? AlertLevel { get; init; }
}

public sealed class TelemetryScenarioCommand
{
    public string ActionKey { get; init; } = "landing-gear";
    public IReadOnlyList<int> ExpectedScanCodes { get; init; } = [];
    public TelemetryScenarioUiExpectation? ExpectedUi { get; init; }
}

public sealed class TelemetryScenarioPanelExpectation
{
    public string? StatusKey { get; init; }
    public bool? IsAvailable { get; init; }
    public IReadOnlyDictionary<string, TelemetryScenarioAlertExpectation> Alerts { get; init; } =
        new Dictionary<string, TelemetryScenarioAlertExpectation>();
}

public sealed class TelemetryScenarioAlertExpectation
{
    public string? Label { get; init; }
    public string? Value { get; init; }
    public string? StatusKey { get; init; }
    public string? AlertLevel { get; init; }
    public bool? IsAvailable { get; init; }
    public float? NumericValue { get; init; }
}
