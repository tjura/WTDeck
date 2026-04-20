using System.Security.Cryptography;
using System.Text;

namespace WTDeck.StreamDock.Profiles;

/// <summary>
/// Produces deterministic v5-style UUIDs from a seed + name.
/// Used so WTDeck profile and action IDs stay stable across re-installs.
/// </summary>
public static class DeterministicUuid
{
    public static Guid Create(string @namespace, string name)
    {
        var input = Encoding.UTF8.GetBytes($"{@namespace}:{name}");
        var hash = SHA1.HashData(input);

        // Build RFC 4122 UUID bytes (big-endian / canonical form)
        var rfcBytes = new byte[16];
        Array.Copy(hash, rfcBytes, 16);

        // Set version to 5 (name-based SHA-1) and variant to RFC 4122
        rfcBytes[6] = (byte)((rfcBytes[6] & 0x0F) | 0x50);
        rfcBytes[8] = (byte)((rfcBytes[8] & 0x3F) | 0x80);

        // Build the canonical hex string: 8-4-4-4-12
        var hex = Convert.ToHexString(rfcBytes);
        var formatted =
            $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..32]}";

        return Guid.ParseExact(formatted, "D");
    }
}
