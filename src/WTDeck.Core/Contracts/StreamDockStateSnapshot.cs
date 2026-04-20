namespace WTDeck.Core.Contracts;

public sealed record StreamDockStateSnapshot(
    int ProtocolVersion,
    string AppVersion,
    DateTimeOffset Timestamp,
    StreamDockState State);
