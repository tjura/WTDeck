namespace WTDeck.Core.Contracts;

public sealed record ButtonPressCommand(
    int ProtocolVersion,
    string ActionKey);
