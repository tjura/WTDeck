using FluentAssertions;
using WTDeck.Core.Models;

namespace WTDeck.Core.Tests.Models;

public class FlightSnapshotTests
{
    [Fact]
    public void Snapshots_with_same_values_are_equal()
    {
        var ts = DateTimeOffset.UtcNow;
        var a = new FlightSnapshot
        {
            Valid = true,
            Timestamp = ts,
            AircraftType = "a_4n",
            GearPercent = 100f,
            IndicatedAirspeedKmh = 500f,
        };
        var b = new FlightSnapshot
        {
            Valid = true,
            Timestamp = ts,
            AircraftType = "a_4n",
            GearPercent = 100f,
            IndicatedAirspeedKmh = 500f,
        };
        a.Should().Be(b);
    }

    [Fact]
    public void Snapshots_with_different_gear_are_not_equal()
    {
        var ts = DateTimeOffset.UtcNow;
        var a = new FlightSnapshot { Valid = true, Timestamp = ts, GearPercent = 0f };
        var b = new FlightSnapshot { Valid = true, Timestamp = ts, GearPercent = 100f };
        a.Should().NotBe(b);
    }

    [Fact]
    public void Null_aircraft_type_is_allowed()
    {
        var snap = new FlightSnapshot { Valid = true, Timestamp = DateTimeOffset.UtcNow };
        snap.AircraftType.Should().BeNull();
    }

    [Fact]
    public void Gear_derived_property_is_gearPercent_divided_by_100()
    {
        var snap = new FlightSnapshot { Valid = true, Timestamp = DateTimeOffset.UtcNow, GearPercent = 41f };
        snap.Gear.Should().BeApproximately(0.41f, 0.0001f);
    }

    [Fact]
    public void Fuel_fraction_handles_zero_initial_mass()
    {
        var snap = new FlightSnapshot { Valid = true, Timestamp = DateTimeOffset.UtcNow };
        snap.FuelFraction.Should().Be(0f);
    }

    [Fact]
    public void Fuel_fraction_clamps_to_1()
    {
        var snap = new FlightSnapshot
        {
            Valid = true,
            Timestamp = DateTimeOffset.UtcNow,
            FuelMassKg = 200f,
            FuelMassInitialKg = 100f,
        };
        snap.FuelFraction.Should().Be(1f);
    }
}
