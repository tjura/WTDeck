using System.Collections.Concurrent;
using WTDeck.Core.Models;

namespace WTDeck.Core.Alerts;

public sealed class AlertCenter : IAlertCenter
{
    private readonly IAlertActionBindingRegistry _bindings;
    private readonly ConcurrentDictionary<string, Alert> _alerts = new(StringComparer.Ordinal);

    public AlertCenter(IAlertActionBindingRegistry bindings)
    {
        _bindings = bindings;
    }

    public Alert Raise(string key, AlertLevel severity, string message, DateTimeOffset now)
    {
        return _alerts.AddOrUpdate(
            key,
            addValueFactory: _ => new Alert(key, severity, AlertStatus.Active, message, now, AcknowledgedAt: null),
            updateValueFactory: (_, existing) =>
            {
                // Same severity: preserve existing alert (including Acknowledged phase).
                // Per user decision, an acknowledged alert is NOT re-raised on worsening -
                // so we do nothing even if severity would escalate.
                return existing;
            });
    }

    public void Clear(string key)
    {
        _alerts.TryRemove(key, out _);
    }

    public bool Acknowledge(string actionKey, DateTimeOffset now)
    {
        var alertKeys = _bindings.AlertKeysForAction(actionKey);
        if (alertKeys.Count == 0)
            return false;

        var acknowledgedAny = false;
        foreach (var alertKey in alertKeys)
        {
            if (!_alerts.TryGetValue(alertKey, out var existing))
                continue;

            if (existing.Status != AlertStatus.Active)
                continue;

            var demoted = existing with
            {
                Status = AlertStatus.Acknowledged,
                AcknowledgedAt = now
            };

            if (_alerts.TryUpdate(alertKey, demoted, existing))
                acknowledgedAny = true;
        }

        return acknowledgedAny;
    }

    public IReadOnlyCollection<Alert> Current => _alerts.Values.ToList().AsReadOnly();

    public Alert? Find(string key) =>
        _alerts.TryGetValue(key, out var alert) ? alert : null;
}
