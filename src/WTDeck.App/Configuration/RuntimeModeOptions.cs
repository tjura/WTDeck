namespace WTDeck.App.Configuration;

public sealed class RuntimeModeOptions
{
    public bool DebugEnabled { get; init; }
    public bool EmulateApi { get; init; }
    public string? ScenarioPath { get; init; }

    public bool DisableSideEffects => DebugEnabled;
    public bool UseTray => !DebugEnabled;
}
