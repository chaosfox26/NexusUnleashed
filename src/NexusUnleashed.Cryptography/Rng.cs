using System.Security.Cryptography;

namespace NexusUnleashed.Cryptography;

public static class Rng
{
    public static byte[] GenerateRandomKey(int length)
    {
        var key = new byte[length];
        RandomNumberGenerator.Fill(key);
        return key;
    }
}
