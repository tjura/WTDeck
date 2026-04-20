namespace WTDeck.Core.Profiles;

public sealed class AircraftProfileRegistry : IAircraftProfileRegistry
{
    private readonly Dictionary<string, AircraftProfile> _byId;

    public AircraftProfileRegistry(IEnumerable<AircraftProfile> profiles)
    {
        _byId = profiles
            .Where(p => !string.Equals(p.Id, AircraftProfile.Generic.Id, StringComparison.Ordinal))
            .ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
    }

    public AircraftProfile Resolve(string? aircraftType)
    {
        if (string.IsNullOrEmpty(aircraftType))
            return AircraftProfile.Generic;

        return _byId.TryGetValue(aircraftType, out var profile)
            ? profile
            : AircraftProfile.Generic;
    }

    public IReadOnlyCollection<AircraftProfile> All => _byId.Values;
}
