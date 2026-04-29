namespace WTDeck.Core.Contracts;

public sealed record StreamDockActionState(
    string StatusKey,
    string Title,
    bool IsBlinking,
    bool IsEnabled,
    string AlertLevel);
