namespace WTDeck.Core.Alerts;

/// <summary>
/// Canonical alert key identifiers. One per distinct warning condition.
/// Keys are bound to button action keys via <see cref="IAlertActionBindingRegistry"/>.
/// </summary>
public static class AlertKey
{
    public const string GearOverspeed = "gear-overspeed";

    // Future alert keys (reserved, not yet implemented):
    public const string FlapsOverspeed = "flaps-overspeed";
    public const string Vne = "vne-exceeded";
    public const string GLimit = "g-limit-exceeded";
    public const string AoAStall = "aoa-stall";
}
