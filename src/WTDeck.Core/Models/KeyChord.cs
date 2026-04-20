namespace WTDeck.Core.Models;

public sealed record KeyChord(IReadOnlyList<int> ScanCodes)
{
    public bool Equals(KeyChord? other)
    {
        if (other is null) return false;
        return ScanCodes.SequenceEqual(other.ScanCodes);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var code in ScanCodes)
            hash.Add(code);
        return hash.ToHashCode();
    }
}
