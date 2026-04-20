using System.Text.Json;
using System.Text.Json.Serialization;

namespace WTDeck.Telemetry;

/// <summary>
/// DTO for the /state endpoint.
///
/// Field names use War Thunder's exact format including spaces, commas, and
/// percent signs (e.g. "gear, %"). Unknown fields are captured by
/// <see cref="Extra"/> via <see cref="JsonExtensionDataAttribute"/> so
/// per-engine and per-tank keys like "throttle 1, %" or "Mfuel 1, kg" can be
/// extracted by index in <see cref="TelemetryMapper"/>.
/// </summary>
internal sealed class StateResponse
{
    [JsonPropertyName("valid")]
    public bool Valid { get; init; }

    // Flight parameters
    [JsonPropertyName("H, m")]
    public float AltitudeMeters { get; init; }

    [JsonPropertyName("TAS, km/h")]
    public float TrueAirspeedKmh { get; init; }

    [JsonPropertyName("IAS, km/h")]
    public float IndicatedAirspeedKmh { get; init; }

    [JsonPropertyName("M")]
    public float MachNumber { get; init; }

    [JsonPropertyName("AoA, deg")]
    public float AoaDeg { get; init; }

    [JsonPropertyName("AoS, deg")]
    public float AoSDeg { get; init; }

    [JsonPropertyName("Ny")]
    public float LoadFactorNy { get; init; }

    [JsonPropertyName("Vy, m/s")]
    public float VerticalSpeedMs { get; init; }

    [JsonPropertyName("Wx, m/s")]
    public float WindXMs { get; init; }

    // Control surfaces
    [JsonPropertyName("aileron, %")]
    public float AileronPercent { get; init; }

    [JsonPropertyName("elevator, %")]
    public float ElevatorPercent { get; init; }

    [JsonPropertyName("rudder, %")]
    public float RudderPercent { get; init; }

    [JsonPropertyName("flaps, %")]
    public float FlapsPercent { get; init; }

    [JsonPropertyName("gear, %")]
    public float GearPercent { get; init; }

    [JsonPropertyName("airbrake, %")]
    public float AirbrakePercent { get; init; }

    // Fuel
    [JsonPropertyName("Mfuel, kg")]
    public float FuelMassKg { get; init; }

    [JsonPropertyName("Mfuel0, kg")]
    public float FuelMassInitialKg { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}
