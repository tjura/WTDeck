using WTDeck.Core.Models;

namespace WTDeck.Core.Tests.TestDoubles;

/// <summary>
/// Fluent builder for creating <see cref="FlightSnapshot"/> instances in tests
/// without spelling out ~40 parameters per call. Defaults to a valid, level
/// flight condition so tests can override only the fields they care about.
/// </summary>
public sealed class FlightSnapshotBuilder
{
    private bool _valid = true;
    private DateTimeOffset _timestamp = DateTimeOffset.UtcNow;
    private string? _aircraftType = "test-aircraft";
    private float _gearPercent;
    private float _iasKmh;
    private float _aoaDeg;
    private float _altitudeMeters;
    private float _machNumber;
    private float _loadFactorNy = 1f;
    private float _flapsPercent;
    private float _airbrakePercent;
    private float _gearsLamp;
    private float _gearsCommand;
    private int? _flaresRemaining;

    public FlightSnapshotBuilder Invalid() { _valid = false; return this; }
    public FlightSnapshotBuilder WithType(string? type) { _aircraftType = type; return this; }
    public FlightSnapshotBuilder WithGearPercent(float percent) { _gearPercent = percent; return this; }
    public FlightSnapshotBuilder WithIas(float kmh) { _iasKmh = kmh; return this; }
    public FlightSnapshotBuilder WithAoa(float deg) { _aoaDeg = deg; return this; }
    public FlightSnapshotBuilder WithAltitude(float meters) { _altitudeMeters = meters; return this; }
    public FlightSnapshotBuilder WithMach(float m) { _machNumber = m; return this; }
    public FlightSnapshotBuilder WithNy(float ny) { _loadFactorNy = ny; return this; }
    public FlightSnapshotBuilder WithFlapsPercent(float percent) { _flapsPercent = percent; return this; }
    public FlightSnapshotBuilder WithAirbrakePercent(float percent) { _airbrakePercent = percent; return this; }
    public FlightSnapshotBuilder WithGearsLamp(float value) { _gearsLamp = value; return this; }
    public FlightSnapshotBuilder WithGearsCommand(float value) { _gearsCommand = value; return this; }
    public FlightSnapshotBuilder WithFlaresRemaining(int? value) { _flaresRemaining = value; return this; }
    public FlightSnapshotBuilder WithTimestamp(DateTimeOffset ts) { _timestamp = ts; return this; }

    public FlightSnapshot Build() => new()
    {
        Valid = _valid,
        Timestamp = _timestamp,
        AircraftType = _aircraftType,
        GearPercent = _gearPercent,
        IndicatedAirspeedKmh = _iasKmh,
        AoaDeg = _aoaDeg,
        AltitudeMeters = _altitudeMeters,
        MachNumber = _machNumber,
        LoadFactorNy = _loadFactorNy,
        FlapsPercent = _flapsPercent,
        AirbrakePercent = _airbrakePercent,
        GearsLamp = _gearsLamp,
        GearsCommand = _gearsCommand,
        FlaresRemaining = _flaresRemaining,
    };

    public static FlightSnapshot Build(Action<FlightSnapshotBuilder> configure)
    {
        var b = new FlightSnapshotBuilder();
        configure(b);
        return b.Build();
    }
}
