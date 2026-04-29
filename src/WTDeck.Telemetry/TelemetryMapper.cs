using System.Text.Json;
using WTDeck.Core.Models;

namespace WTDeck.Telemetry;

/// <summary>
/// Builds a <see cref="FlightSnapshot"/> from the two War Thunder endpoints.
///
/// Rule of thumb for field precedence:
///   - /state is authoritative for flight parameters (IAS, TAS, altitude) and
///     physical control surface positions (gear %, flaps %).
///   - /indicators is authoritative for cockpit gauges and lever commands.
///
/// Long tail fields (blister*, aoa_indexer*, weapon2/4, per-engine "throttle 1, %",
/// per-tank "Mfuel 1, kg") are captured from the [JsonExtensionData] dictionaries
/// on the DTOs and flowed into <see cref="FlightSnapshot.RawState"/> /
/// <see cref="FlightSnapshot.RawIndicators"/> so any rule can query any value.
/// Per-engine and per-tank values are also extracted into strongly-typed
/// <see cref="EngineState"/> and <see cref="FuelTank"/> lists.
/// </summary>
internal static class TelemetryMapper
{
    public static FlightSnapshot ToSnapshot(IndicatorsResponse? indicators, StateResponse? state)
    {
        var valid = (state?.Valid ?? false) || (indicators?.Valid ?? false);

        var rawState = state?.Extra is null
            ? new Dictionary<string, float>()
            : FlattenNumeric(state.Extra);

        var rawIndicators = indicators?.Extra is null
            ? new Dictionary<string, float>()
            : FlattenNumeric(indicators.Extra);

        var engines = ExtractEngines(rawState);
        var fuelTanks = ExtractFuelTanks(rawState);

        return new FlightSnapshot
        {
            Valid = valid,
            Timestamp = DateTimeOffset.UtcNow,
            AircraftType = indicators?.Type,
            Army = ParseArmy(indicators?.Army),

            // ----- from /state -----
            AltitudeMeters = state?.AltitudeMeters ?? 0f,
            TrueAirspeedKmh = state?.TrueAirspeedKmh ?? 0f,
            IndicatedAirspeedKmh = state?.IndicatedAirspeedKmh ?? 0f,
            MachNumber = state?.MachNumber ?? 0f,
            AoaDeg = state?.AoaDeg ?? 0f,
            AoSDeg = state?.AoSDeg ?? 0f,
            LoadFactorNy = state?.LoadFactorNy ?? 0f,
            VerticalSpeedMs = state?.VerticalSpeedMs ?? 0f,
            WindXMs = state?.WindXMs ?? 0f,
            AileronPercent = state?.AileronPercent ?? 0f,
            ElevatorPercent = state?.ElevatorPercent ?? 0f,
            RudderPercent = state?.RudderPercent ?? 0f,
            FlapsPercent = state?.FlapsPercent ?? 0f,
            GearPercent = (state is { Valid: true }) ? state.GearPercent : DerivedGearPercent(indicators),
            AirbrakePercent = state?.AirbrakePercent ?? 0f,
            FuelMassKg = state?.FuelMassKg ?? 0f,
            FuelMassInitialKg = state?.FuelMassInitialKg ?? 0f,
            Engines = engines,
            FuelTanks = fuelTanks,

            // ----- from /indicators -----
            RadioAltitudeMeters = indicators?.RadioAltitudeMeters ?? 0f,
            AviahorizonRollDeg = indicators?.AviahorizonRollDeg ?? 0f,
            AviahorizonPitchDeg = indicators?.AviahorizonPitchDeg ?? 0f,
            BankDeg = indicators?.BankDeg ?? 0f,
            TurnRateDegSec = indicators?.TurnRateDegSec ?? 0f,
            CompassHeadingDeg = indicators?.CompassHeadingDeg ?? 0f,
            Clock = BuildClock(indicators),
            OilPressureKgcm2 = indicators?.OilPressureKgcm2 ?? 0f,
            WaterTemperatureC = indicators?.WaterTemperatureC ?? 0f,
            FuelIndicator = indicators?.FuelIndicator ?? 0f,
            FuelConsumeKgh = indicators?.FuelConsumeKgh ?? 0f,
            AirbrakeLever = indicators?.AirbrakeLever ?? 0f,
            GearsCommand = indicators?.GearsCommand ?? 0f,
            GearsLamp = indicators?.GearsLamp ?? 0f,
            FlapsLever = indicators?.FlapsLever ?? 0f,
            FlapsIndicator = indicators?.FlapsIndicator ?? 0f,
            TrimmerLever = indicators?.TrimmerLever ?? 0f,
            TrimmerIndicator = indicators?.TrimmerIndicator ?? 0f,
            Throttle = indicators?.Throttle ?? 0f,
            MachIndicator = indicators?.MachIndicator ?? 0f,
            GMeter = indicators?.GMeter ?? 0f,
            GMeterMin = indicators?.GMeterMin ?? 0f,
            GMeterMax = indicators?.GMeterMax ?? 0f,
            AoaIndicator = indicators?.AoaIndicator ?? 0f,
            FlaresRemaining = ExtractExplicitFlareCount(rawIndicators) ?? ExtractExplicitFlareCount(rawState),

            RawState = rawState,
            RawIndicators = rawIndicators,
        };
    }

    /// <summary>
    /// Falls back to /indicators "gears" x 100 when /state is unavailable. This
    /// keeps existing behaviour (pre-/state support) working if /state ever
    /// goes dark, but the value only represents the handle command, not the
    /// actual gear position.
    /// </summary>
    private static float DerivedGearPercent(IndicatorsResponse? indicators)
        => indicators is null ? 0f : indicators.GearsCommand * 100f;

    private static ArmyType ParseArmy(string? army) => army?.ToLowerInvariant() switch
    {
        "air" => ArmyType.Air,
        "ground" => ArmyType.Ground,
        "ship" => ArmyType.Ship,
        _ => ArmyType.Unknown,
    };

    private static TimeSpan BuildClock(IndicatorsResponse? indicators)
    {
        if (indicators is null) return TimeSpan.Zero;
        return new TimeSpan(
            hours: (int)indicators.ClockHour,
            minutes: (int)indicators.ClockMin,
            seconds: (int)indicators.ClockSec);
    }

    private static Dictionary<string, float> FlattenNumeric(Dictionary<string, JsonElement> extra)
    {
        var result = new Dictionary<string, float>(extra.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in extra)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var f))
                result[key] = f;
        }
        return result;
    }

    private static int? ExtractExplicitFlareCount(IReadOnlyDictionary<string, float> raw)
    {
        foreach (var key in new[] { "flares", "flare", "countermeasures_flares", "countermeasure_flares" })
        {
            if (raw.TryGetValue(key, out var value))
                return Math.Max(0, (int)MathF.Round(value));
        }

        return null;
    }

    /// <summary>
    /// War Thunder flattens per-engine telemetry into indexed keys like
    /// "throttle 1, %", "RPM 1", "oil temp 1, C". Walk them by index until we
    /// stop finding throttle entries.
    /// </summary>
    private static IReadOnlyList<EngineState> ExtractEngines(IReadOnlyDictionary<string, float> raw)
    {
        var engines = new List<EngineState>();
        for (var i = 1; i <= 8; i++)
        {
            if (!raw.TryGetValue($"throttle {i}, %", out var throttle))
                break;

            engines.Add(new EngineState(
                Index: i,
                ThrottlePercent: throttle,
                Rpm: raw.GetValueOrDefault($"RPM {i}"),
                OilTemperatureC: raw.GetValueOrDefault($"oil temp {i}, C"),
                ThrustKgf: raw.GetValueOrDefault($"thrust {i}, kgs"),
                PowerHp: raw.GetValueOrDefault($"power {i}, hp"),
                EfficiencyPercent: raw.GetValueOrDefault($"efficiency {i}, %"),
                ManifoldPressureAtm: raw.GetValueOrDefault($"manifold pressure {i}, atm")));
        }
        return engines;
    }

    /// <summary>
    /// Same pattern for per-tank fuel: "Mfuel 1, kg" and "Mfuel0 1, kg".
    /// </summary>
    private static IReadOnlyList<FuelTank> ExtractFuelTanks(IReadOnlyDictionary<string, float> raw)
    {
        var tanks = new List<FuelTank>();
        for (var i = 1; i <= 8; i++)
        {
            if (!raw.TryGetValue($"Mfuel {i}, kg", out var mass))
                break;

            tanks.Add(new FuelTank(
                Index: i,
                MassKg: mass,
                MassInitialKg: raw.GetValueOrDefault($"Mfuel0 {i}, kg")));
        }
        return tanks;
    }
}
