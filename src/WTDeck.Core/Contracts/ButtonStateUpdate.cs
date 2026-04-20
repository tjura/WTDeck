namespace WTDeck.Core.Contracts;

public sealed record ButtonStateUpdate(
    int ProtocolVersion,
    string ActionKey,
    string Title,
    string? IconBase64,
    bool IsBlinking,
    bool IsEnabled,
    string AlertLevel,
    string? StatusKey = null);
