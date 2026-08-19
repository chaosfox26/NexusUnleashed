// NexusUnleashed - clean-room authored. Carbine's WildStar packet cipher, an
// independent implementation of a PROTOCOL FACT (the client runs the identical
// cipher, so it is uncopyrightable procedure, not anyone's creative expression).
// It is NOT ARC4: a 128-byte key table is expanded from an 8-byte keyInteger via
// two multiply-chains, then each byte is XORed with an 8-byte register + a
// rotating key block (CFB-style). The cipher is STATELESS per message (each
// Encrypt/Decrypt starts from the same key table + register); one PacketCrypt
// instance handles the whole phase.
//
// TWO-PHASE KEYING (solved 2026-08-19, confirmed against the world stream):
// a connection uses TWO keys, each a `keyInteger` fed to this class:
//   * AUTH phase  -> GetKeyFromAuthBuildAndMessage()  (a build constant,
//                    0xD283F5B34A8DC685). Used for the pre-login hello.
//   * WORLD phase -> GetKeyFromTicket(sessionKey)     (folds the 16-byte SRP
//                    session key). Re-keyed after login; all world messages use
//                    it. Recovered key table from the capture rebuilds EXACTLY
//                    from a keyInteger, and this key decrypts the whole world
//                    entry stream (0x0988/0x098B/... byte-for-byte).
// The earlier "stateful, only msg #0" note was WRONG: those later 49-byte frames
// were different messages under the WORLD key, not the hello under a moving key.
using System;

namespace NexusUnleashed.Cryptography;

public sealed class PacketCrypt
{
    // Cipher parameters - facts about Carbine's protocol.
    private const ulong SeedInitial = 8182381946860333969ul;
    private const ulong Multiplier  = 2860486313ul;
    private const uint  LengthSeed  = 2860486314u;   // == (uint)(-1434480982)

    private readonly byte[] _key = new byte[128];
    private readonly ulong _register;

    public PacketCrypt(ulong seed)
    {
        ulong a = SeedInitial;
        ulong b = (a + seed) * Multiplier;
        for (int i = 0; i < 128; i += 8)
        {
            WriteU64(_key, i, b);
            a = (a + b) * Multiplier;
            b = (seed + b) * Multiplier;
        }
        _register = a;
    }

    /// <summary>Server-side encrypt (what the realm sends to the client).</summary>
    public byte[] Encrypt(byte[] data, int length)
    {
        var outp = new byte[length];
        var fb = BitConverter.GetBytes(_register);
        uint counter = LengthSeed * (uint)length;
        uint block = 0;
        for (int i = 0; i < length; i++)
        {
            int k = i % 8;
            if (k == 0) block = (counter++ & 0xF) * 8;
            outp[i] = (byte)(fb[k] ^ data[i] ^ _key[block + k]);
            fb[k] = outp[i];
        }
        return outp;
    }

    /// <summary>Mirror of the client's encrypt (client-&gt;server decrypt).</summary>
    public byte[] Decrypt(byte[] data, int length)
    {
        var outp = new byte[length];
        var fb = BitConverter.GetBytes(_register);
        Array.Reverse(fb);
        uint counter = LengthSeed * (uint)length;
        uint block = 0;
        for (int i = 0; i < length; i++)
        {
            int k = i % 8;
            if (k == 0) block = (counter++ & 0xF) * 8;
            outp[i] = (byte)(fb[7 - k] ^ data[i] ^ _key[block + k]);
            fb[7 - k] = data[i];
        }
        return outp;
    }

    public byte[] Encrypt(byte[] data) => Encrypt(data, data.Length);
    public byte[] Decrypt(byte[] data) => Decrypt(data, data.Length);

    /// <summary>
    /// The AUTH-phase keyInteger: the static build key the channel opens with
    /// (the hello), OBSERVED at runtime on the wire — a clean protocol fact.
    /// </summary>
    /// <remarks>
    /// Provenance: this VALUE was observed at runtime (key-log tap + the captured
    /// keystream it reproduces). We state the value directly. An earlier decomposed
    /// form (`N * Multiplier`) was removed 2026-08-19 under the No-NF law
    /// (provenance/NO-NF.md): that factoring had been read from the NF-derived
    /// `recovered/` tree, and provenance beats convenience.
    /// </remarks>
    public const ulong AuthChannelKey = 0xD283F5B34A8DC685ul;

    // The WORLD-phase keyInteger derivation (session key -> world key) is
    // QUARANTINED, not implemented here. Its formula had been read from the
    // NF-derived recovered tree; under the No-NF law it must be re-sourced from a
    // CLEAN source (the 16042 client's own crypto, or cryptanalysis of a capture
    // whose session key we know) before it ships. See provenance/QUARANTINE-NF.md.
    // The world channel + container codec + message models are all clean (they
    // come from your captures); only this one derivation waits. Until then the
    // world key is supplied to the session directly as a keyInteger.

    private static void WriteU64(byte[] dst, int off, ulong v)
    {
        for (int i = 0; i < 8; i++) dst[off + i] = (byte)(v >> (i * 8));
    }
}
