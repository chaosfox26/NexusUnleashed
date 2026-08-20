// cipher, so it is uncopyrightable procedure, not anyone's creative expression).
using System;

namespace NexusUnleashed.Cryptography;

public sealed class PacketCrypt
{
    private const ulong SeedInitial = 8182381946860333969ul;
    private const ulong Multiplier  = 2860486313ul;
    private const uint  LengthSeed  = 2860486314u;
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

    public byte[] EncryptForClient(byte[] data, int length)
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
            fb[7 - k] = outp[i];        }
        return outp;
    }

    public byte[] Encrypt(byte[] data) => Encrypt(data, data.Length);
    public byte[] Decrypt(byte[] data) => Decrypt(data, data.Length);
    public byte[] EncryptForClient(byte[] data) => EncryptForClient(data, data.Length);

    public const ulong AuthChannelKey = 0xD283F5B34A8DC685ul;


    private static void WriteU64(byte[] dst, int off, ulong v)
    {
        for (int i = 0; i < 8; i++) dst[off + i] = (byte)(v >> (i * 8));
    }
}
