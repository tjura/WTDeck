namespace WTDeck.Core.Profiles.Aircraft;

/// <summary>
/// Douglas A-4N Skyhawk (Israeli variant).
///
/// Only the landing-gear operating speed is validated - all other limits
/// inherit <see cref="AircraftProfile.Generic"/> defaults (effectively disabled)
/// until real in-game values are collected per aircraft.
/// </summary>
public static class A4NSkyhawkProfile
{
    public const string TypeKey = "a_4n";

    public static AircraftProfile Instance { get; } = AircraftProfile.Generic with
    {
        Id = TypeKey,
        DisplayName = "A-4N Skyhawk",
        GearOperatingSpeedKmh = 450f,
    };
}
