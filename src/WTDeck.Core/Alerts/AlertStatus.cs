namespace WTDeck.Core.Alerts;

public enum AlertStatus
{
    /// <summary>Alert is blinking + playing sound - needs pilot attention.</summary>
    Active,

    /// <summary>Pilot pressed the associated button. Visual stays, blink + sound stop.</summary>
    Acknowledged,

    /// <summary>Condition resolved. Alert has been or will be removed.</summary>
    Cleared
}
