namespace WTDeck.Core.Profiles;

/// <summary>
/// Operating limits for a specific aircraft.
///
/// Rules consult the aircraft profile to decide when to raise warnings.
/// Unknown aircraft fall back to <see cref="Generic"/> which uses conservative
/// defaults high enough that most rules stay silent until a real profile
/// is registered.
/// </summary>
public sealed record AircraftProfile(
    string Id,
    string DisplayName,
    float GearOperatingSpeedKmh,
    float FlapsOperatingSpeedKmh,
    float VneIasKmh,
    float VneMach,
    float MaxPositiveG,
    float OverGWarningThreshold,
    float OverGDangerThreshold,
    float MaxNegativeG,
    float CriticalAoADeg,
    float AirbrakeOperatingSpeedKmh,
    bool HasFlares,
    int? DefaultFlares)
{
    /// <summary>
    /// Fallback profile for unknown aircraft. Values are intentionally high so
    /// rules using them stay quiet - a real profile must be registered for any
    /// aircraft that wants meaningful warnings.
    /// </summary>
    public static AircraftProfile Generic { get; } = new(
        Id: "_generic",
        DisplayName: "Generic",
        GearOperatingSpeedKmh: 9999f,
        FlapsOperatingSpeedKmh: 9999f,
        VneIasKmh: 9999f,
        VneMach: 9.99f,
        MaxPositiveG: 99f,
        OverGWarningThreshold: 99f,
        OverGDangerThreshold: 999f,
        MaxNegativeG: -99f,
        CriticalAoADeg: 90f,
        AirbrakeOperatingSpeedKmh: 9999f,
        HasFlares: false,
        DefaultFlares: null);
}
