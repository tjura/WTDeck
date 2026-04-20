namespace WTDeck.Core.Contracts;

public sealed record HandshakeMessage(
    int ProtocolVersion,
    string AppVersion);
