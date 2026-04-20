namespace WTDeck.Core.Contracts;

public sealed record StreamDockState(
    string GearStatus,
    string GearTitle,
    bool GearBlinking,
    string GearAlertLevel);
