// NexusUnleashed - clean-room authored. The game-channel packet cipher, our own
// code over the MIT Arctium ARC4 primitive. Behavior established by OBSERVING
// the oracle (not by reading its code):
//   * one ARC4 stream per connection (state carries across all messages),
//   * keyed by a STATIC 8-byte value derived from the client build (the realm
//     sets it once at accept, before SRP, and never re-keys),
//   * the key value itself is observed at runtime (packet-key.log) - a fact -
//     never lifted from any emulator's derivation code.
// Verified by reproducing a real captured keystream (see the crypto tests).
using System;

namespace NexusUnleashed.Cryptography;

public sealed class PacketCrypt
{
    private readonly ARC4 _cipher = new ARC4();

    /// <param name="key">The static packet key (observed from the running realm).</param>
    /// <param name="bigEndian">Byte order of the key; empirically matched against a captured keystream.</param>
    public PacketCrypt(ulong key, bool bigEndian = false)
        => _cipher.PrepareKey(KeyBytes(key, bigEndian));

    public PacketCrypt(byte[] key) => _cipher.PrepareKey(key);

    /// <summary>Encrypt in place; advances the stream (ARC4 is symmetric).</summary>
    public void Encrypt(byte[] data) => _cipher.ProcessBuffer(data);

    /// <summary>Decrypt in place; advances the stream.</summary>
    public void Decrypt(byte[] data) => _cipher.ProcessBuffer(data);

    private static byte[] KeyBytes(ulong key, bool bigEndian)
    {
        var b = BitConverter.GetBytes(key);            // little-endian on x64
        if (bigEndian) Array.Reverse(b);
        return b;
    }
}
