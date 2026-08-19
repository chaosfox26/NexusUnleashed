// NexusUnleashed - clean-room authored (Rng): a standard cryptographic random
// key generator. Provenance: authored here; the algorithm (fill N bytes from a
// CSPRNG) is a fact, not anyone's expression.
using System.Security.Cryptography;

namespace NexusUnleashed.Cryptography;

/// <summary>Cryptographically-secure random key material.</summary>
public static class Rng
{
    public static byte[] GenerateRandomKey(int length)
    {
        var key = new byte[length];
        RandomNumberGenerator.Fill(key);
        return key;
    }
}
