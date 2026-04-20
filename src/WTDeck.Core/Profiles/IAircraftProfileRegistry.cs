namespace WTDeck.Core.Profiles;

public interface IAircraftProfileRegistry
{
    /// <summary>
    /// Resolves an aircraft profile by its War Thunder type identifier (e.g. "a_4n").
    /// Returns <see cref="AircraftProfile.Generic"/> when the type is unknown or null.
    /// </summary>
    AircraftProfile Resolve(string? aircraftType);

    IReadOnlyCollection<AircraftProfile> All { get; }
}
