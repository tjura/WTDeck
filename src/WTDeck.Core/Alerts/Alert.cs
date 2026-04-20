using WTDeck.Core.Models;

namespace WTDeck.Core.Alerts;

public sealed record Alert(
    string Key,
    AlertLevel Severity,
    AlertStatus Status,
    string Message,
    DateTimeOffset RaisedAt,
    DateTimeOffset? AcknowledgedAt);
