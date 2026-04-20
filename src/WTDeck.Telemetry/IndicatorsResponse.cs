using System.Text.Json;
using System.Text.Json.Serialization;

namespace WTDeck.Telemetry;

/// <summary>
/// DTO for the /indicators endpoint.
///
/// The strongly-typed fields are the subset of cockpit instruments that rules
/// care about today. The remaining long tail (blister1-12, aoa_indexer1-3,
/// weapon2/4, and any future unknown fields) is captured by <see cref="Extra"/>
/// via <see cref="JsonExtensionDataAttribute"/> and forwarded into
/// <see cref="Models.FlightSnapshot.RawIndicators"/>.
/// </summary>
internal sealed class IndicatorsResponse
{
    [JsonPropertyName("valid")]
    public bool Valid { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("army")]
    public string? Army { get; init; }

    [JsonPropertyName("speed")]
    public float SpeedMs { get; init; }

    [JsonPropertyName("vario")]
    public float Vario { get; init; }

    // Altitude variants
    [JsonPropertyName("altitude_hour")]
    public float AltitudeHour { get; init; }

    [JsonPropertyName("altitude_min")]
    public float AltitudeMin { get; init; }

    [JsonPropertyName("altitude_10k")]
    public float Altitude10k { get; init; }

    [JsonPropertyName("radio_altitude")]
    public float RadioAltitudeMeters { get; init; }

    // Attitude
    [JsonPropertyName("aviahorizon_roll")]
    public float AviahorizonRollDeg { get; init; }

    [JsonPropertyName("aviahorizon_pitch1")]
    public float AviahorizonPitchDeg { get; init; }

    [JsonPropertyName("bank")]
    public float BankDeg { get; init; }

    [JsonPropertyName("turn")]
    public float TurnRateDegSec { get; init; }

    [JsonPropertyName("compass")]
    public float CompassHeadingDeg { get; init; }

    // Clock (in-game time)
    [JsonPropertyName("clock_hour")]
    public float ClockHour { get; init; }

    [JsonPropertyName("clock_min")]
    public float ClockMin { get; init; }

    [JsonPropertyName("clock_sec")]
    public float ClockSec { get; init; }

    // Engine instruments
    [JsonPropertyName("rpm_min")]
    public float RpmMin { get; init; }

    [JsonPropertyName("rpm_hour")]
    public float RpmHour { get; init; }

    [JsonPropertyName("oil_pressure")]
    public float OilPressureKgcm2 { get; init; }

    [JsonPropertyName("water_temperature")]
    public float WaterTemperatureC { get; init; }

    // Fuel
    [JsonPropertyName("fuel")]
    public float FuelIndicator { get; init; }

    [JsonPropertyName("fuel_consume")]
    public float FuelConsumeKgh { get; init; }

    // Handle / lever positions and their indicators
    [JsonPropertyName("airbrake_lever")]
    public float AirbrakeLever { get; init; }

    [JsonPropertyName("gears")]
    public float GearsCommand { get; init; }

    [JsonPropertyName("gears_lamp")]
    public float GearsLamp { get; init; }

    [JsonPropertyName("gears_indicator")]
    public float GearsIndicator { get; init; }

    [JsonPropertyName("flaps")]
    public float FlapsLever { get; init; }

    [JsonPropertyName("flaps_indicator")]
    public float FlapsIndicator { get; init; }

    [JsonPropertyName("trimmer")]
    public float TrimmerLever { get; init; }

    [JsonPropertyName("trimmer_indicator")]
    public float TrimmerIndicator { get; init; }

    [JsonPropertyName("throttle")]
    public float Throttle { get; init; }

    [JsonPropertyName("mach")]
    public float MachIndicator { get; init; }

    // G-meter
    [JsonPropertyName("g_meter")]
    public float GMeter { get; init; }

    [JsonPropertyName("g_meter_min")]
    public float GMeterMin { get; init; }

    [JsonPropertyName("g_meter_max")]
    public float GMeterMax { get; init; }

    // AoA
    [JsonPropertyName("aoa")]
    public float AoaIndicator { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}
