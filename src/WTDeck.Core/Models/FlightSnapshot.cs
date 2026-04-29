namespace WTDeck.Core.Models;

/// <summary>
/// Full real-time snapshot of War Thunder flight telemetry.
///
/// Strongly-typed fields cover the common parameters used by rules today.
/// <see cref="RawState"/> and <see cref="RawIndicators"/> carry the long tail of
/// less-common fields (blister1-12, aoa_indexer1-3, future unknown fields) as
/// plain name->float maps so any rule can query any value without touching this
/// record.
///
/// Rules should prefer strongly-typed properties where available. Only reach
/// into the raw dictionaries for aircraft-specific or rarely-used values.
/// </summary>
public sealed record FlightSnapshot
{
    public required bool Valid { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    // ---------------- Identity ----------------
    public string? AircraftType { get; init; }
    public ArmyType Army { get; init; } = ArmyType.Unknown;

    // ---------------- /state: flight parameters ----------------
    public float AltitudeMeters { get; init; }
    public float TrueAirspeedKmh { get; init; }
    public float IndicatedAirspeedKmh { get; init; }
    public float MachNumber { get; init; }
    public float AoaDeg { get; init; }
    public float AoSDeg { get; init; }
    public float LoadFactorNy { get; init; }
    public float VerticalSpeedMs { get; init; }
    public float WindXMs { get; init; }

    // ---------------- /state: control surfaces ----------------
    public float AileronPercent { get; init; }
    public float ElevatorPercent { get; init; }
    public float RudderPercent { get; init; }
    public float FlapsPercent { get; init; }

    /// <summary>Actual physical gear extension from /state "gear, %" - range 0..100.</summary>
    public float GearPercent { get; init; }

    public float AirbrakePercent { get; init; }

    // ---------------- /state: fuel + engines ----------------
    public float FuelMassKg { get; init; }
    public float FuelMassInitialKg { get; init; }
    public IReadOnlyList<EngineState> Engines { get; init; } = [];
    public IReadOnlyList<FuelTank> FuelTanks { get; init; } = [];

    // ---------------- /indicators: cockpit instruments ----------------
    public float RadioAltitudeMeters { get; init; }
    public float AviahorizonRollDeg { get; init; }
    public float AviahorizonPitchDeg { get; init; }
    public float BankDeg { get; init; }
    public float TurnRateDegSec { get; init; }
    public float CompassHeadingDeg { get; init; }
    public TimeSpan Clock { get; init; }
    public float OilPressureKgcm2 { get; init; }
    public float WaterTemperatureC { get; init; }
    public float FuelIndicator { get; init; }
    public float FuelConsumeKgh { get; init; }
    public float AirbrakeLever { get; init; }

    /// <summary>Gear handle command from /indicators "gears" - 0 (up) or 1 (down).</summary>
    public float GearsCommand { get; init; }

    /// <summary>Gear warning lamp from /indicators "gears_lamp".</summary>
    public float GearsLamp { get; init; }

    public float FlapsLever { get; init; }
    public float FlapsIndicator { get; init; }
    public float TrimmerLever { get; init; }
    public float TrimmerIndicator { get; init; }
    public float Throttle { get; init; }
    public float MachIndicator { get; init; }
    public float GMeter { get; init; }
    public float GMeterMin { get; init; }
    public float GMeterMax { get; init; }
    public float AoaIndicator { get; init; }

    /// <summary>Explicit flare count from clear telemetry fields; null when unavailable.</summary>
    public int? FlaresRemaining { get; init; }

    // ---------------- Long tail ----------------
    public IReadOnlyDictionary<string, float> RawState { get; init; } =
        new Dictionary<string, float>();
    public IReadOnlyDictionary<string, float> RawIndicators { get; init; } =
        new Dictionary<string, float>();

    // ---------------- Derived convenience ----------------

    /// <summary>Gear position normalized to 0.0..1.0 for threshold comparisons.</summary>
    public float Gear => GearPercent / 100f;

    /// <summary>Fuel remaining as a fraction of initial load (0.0..1.0), clamped.</summary>
    public float FuelFraction =>
        FuelMassInitialKg <= 0f ? 0f : Math.Clamp(FuelMassKg / FuelMassInitialKg, 0f, 1f);

    // ---------------- Value equality ----------------
    // Records with IReadOnlyList/IReadOnlyDictionary don't get sequence equality by default.
    // Override for correct deduplication in AppHost.

    public bool Equals(FlightSnapshot? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Valid == other.Valid
               && Timestamp == other.Timestamp
               && AircraftType == other.AircraftType
               && Army == other.Army
               && AltitudeMeters.Equals(other.AltitudeMeters)
               && TrueAirspeedKmh.Equals(other.TrueAirspeedKmh)
               && IndicatedAirspeedKmh.Equals(other.IndicatedAirspeedKmh)
               && MachNumber.Equals(other.MachNumber)
               && AoaDeg.Equals(other.AoaDeg)
               && LoadFactorNy.Equals(other.LoadFactorNy)
               && GearPercent.Equals(other.GearPercent)
               && FlapsPercent.Equals(other.FlapsPercent)
               && AirbrakePercent.Equals(other.AirbrakePercent)
               && FuelMassKg.Equals(other.FuelMassKg)
               && GearsCommand.Equals(other.GearsCommand)
               && GearsLamp.Equals(other.GearsLamp)
               && FlaresRemaining == other.FlaresRemaining;
        // Intentionally does not compare RawState / RawIndicators dictionaries -
        // that would be O(n) per tick. The fields above are the ones that drive
        // rule decisions; if two snapshots agree on them we treat them as equal
        // for dedup purposes.
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Valid);
        hash.Add(Timestamp);
        hash.Add(AircraftType);
        hash.Add(Army);
        hash.Add(GearPercent);
        hash.Add(IndicatedAirspeedKmh);
        hash.Add(MachNumber);
        hash.Add(AoaDeg);
        hash.Add(LoadFactorNy);
        hash.Add(FlaresRemaining);
        return hash.ToHashCode();
    }
}
