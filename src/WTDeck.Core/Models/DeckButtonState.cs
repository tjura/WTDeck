namespace WTDeck.Core.Models;

public sealed record DeckButtonState(
    string ActionKey,
    string Title,
    string IconKey,
    bool IsBlinking,
    bool IsEnabled,
    AlertLevel AlertLevel);
