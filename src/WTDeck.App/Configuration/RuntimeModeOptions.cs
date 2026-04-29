namespace WTDeck.App.Configuration;

public sealed class RuntimeModeOptions
{
    public bool DebugEnabled { get; init; }
    public bool EmulateApi { get; init; }
    public string? ScenarioPath { get; init; }
    public bool Capture8111 { get; init; }
    public Capture8111Options CaptureOptions { get; init; } = Capture8111Options.Default;
    public string? Analyze8111CaptureDirectory { get; init; }

    public bool Analyze8111Capture => Analyze8111CaptureDirectory is not null;
    public bool DisableSideEffects => DebugEnabled || Capture8111 || Analyze8111Capture;
    public bool UseTray => !DisableSideEffects;
}

public sealed record Capture8111Options(
    string? OutputDirectory,
    int DurationSeconds,
    int IntervalMs,
    int DumpIntervalSeconds)
{
    public static Capture8111Options Default { get; } = new(
        OutputDirectory: null,
        DurationSeconds: 300,
        IntervalMs: 500,
        DumpIntervalSeconds: 10);
}
