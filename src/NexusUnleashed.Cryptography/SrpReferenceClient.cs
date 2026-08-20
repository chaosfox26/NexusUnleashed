using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace NexusUnleashed.Cryptography;

public sealed class SrpClientResult
{
    public byte[] PublicA { get; init; } = Array.Empty<byte>();
    public byte[] ProofM1 { get; init; } = Array.Empty<byte>();
    public byte[] SessionKey { get; init; } = Array.Empty<byte>();
}

public static class SrpReferenceClient
{
    private static readonly byte[] N =
    {
        0xE3, 0x06, 0xEB, 0xC0, 0x2F, 0x1D, 0xC6, 0x9F, 0x5B, 0x43, 0x76, 0x83, 0xFE, 0x38, 0x51, 0xFD,
        0x9A, 0xAA, 0x6E, 0x97, 0xF4, 0xCB, 0xD4, 0x2F, 0xC0, 0x6C, 0x72, 0x05, 0x3C, 0xBC, 0xED, 0x68,
        0xEC, 0x57, 0x0E, 0x66, 0x66, 0xF5, 0x29, 0xC5, 0x85, 0x18, 0xCF, 0x7B, 0x29, 0x9B, 0x55, 0x82,
        0x49, 0x5D, 0xB1, 0x69, 0xAD, 0xF4, 0x8E, 0xCE, 0xB6, 0xD6, 0x54, 0x61, 0xB4, 0xD7, 0xC7, 0x5D,
        0xD1, 0xDA, 0x89, 0x60, 0x1D, 0x5C, 0x49, 0x8E, 0xE4, 0x8B, 0xB9, 0x50, 0xE2, 0xD8, 0xD5, 0xE0,
        0xE0, 0xC6, 0x92, 0xD6, 0x13, 0x48, 0x3B, 0x38, 0xD3, 0x81, 0xEA, 0x96, 0x74, 0xDF, 0x74, 0xD6,
        0x76, 0x65, 0x25, 0x9C, 0x4C, 0x31, 0xA2, 0x9E, 0x0B, 0x3C, 0xFF, 0x75, 0x87, 0x61, 0x72, 0x60,
        0xE8, 0xC5, 0x8F, 0xFA, 0x0A, 0xF8, 0x33, 0x9C, 0xD6, 0x8D, 0xB3, 0xAD, 0xB9, 0x0A, 0xAF, 0xEE
    };
    private static readonly byte[] gBytes = { 2, 0, 0, 0 };

    public static SrpClientResult Respond(string saltHex, string accountName, string password, byte[] serverB)
    {
        using var sha = SHA256.Create();
        BigInteger Nbn = LeToBig(N);
        BigInteger gbn = new BigInteger(2);
        BigInteger k = LeToBig(ReverseUInt32(sha.ComputeHash(Combine(N, gBytes))));

        byte[] I = sha.ComputeHash(Encoding.UTF8.GetBytes(accountName));
        byte[] salt = FromHex(saltHex);

        byte[] p = sha.ComputeHash(Encoding.UTF8.GetBytes(accountName + ":" + password));
        BigInteger x = LeToBig(ReverseUInt32(sha.ComputeHash(Combine(salt, p))));

        byte[] aBytes = Rng.GenerateRandomKey(0x20);
        BigInteger a = LeToBig(aBytes);
        BigInteger A = BigInteger.ModPow(gbn, a, Nbn);
        byte[] Asend = BigToLe(A);

        BigInteger B = LeToBig(serverB);

        BigInteger u = LeToBig(ReverseUInt32(sha.ComputeHash(Combine(Asend, serverB))));

        BigInteger gx = BigInteger.ModPow(gbn, x, Nbn);
        BigInteger baseVal = ((B - k * gx) % Nbn + Nbn) % Nbn;
        BigInteger S = BigInteger.ModPow(baseVal, a + u * x, Nbn);

        byte[] sessionKey = InterleaveSessionKey(GetBytes(BigToLe(S), 0x80), sha);
        byte[] m1 = ComputeM1(I, salt, Asend, serverB, sessionKey, sha);

        return new SrpClientResult { PublicA = Asend, ProofM1 = m1, SessionKey = sessionKey };
    }

    public static string ComputeVerifier(string saltHex, string accountName, string password)
    {
        using var sha = SHA256.Create();
        BigInteger Nbn = LeToBig(N);
        BigInteger gbn = new BigInteger(2);
        byte[] salt = FromHex(saltHex);
        byte[] p = sha.ComputeHash(Encoding.UTF8.GetBytes(accountName + ":" + password));
        BigInteger x = LeToBig(ReverseUInt32(sha.ComputeHash(Combine(salt, p))));
        BigInteger v = BigInteger.ModPow(gbn, x, Nbn);
        return ToHex(BigToLe(v));
    }


    private static BigInteger LeToBig(byte[] le) => new BigInteger(Combine(le, new byte[] { 0 }));

    private static byte[] BigToLe(BigInteger v)
    {
        byte[] b = v.ToByteArray();        return b;
    }

    private static byte[] ReverseUInt32(byte[] data)
    {
        var ret = new byte[data.Length];
        for (var i = 0; i < data.Length; i += 4)
            Buffer.BlockCopy(data, i, ret, ret.Length - (i + 4), 4);
        return ret;
    }

    private static byte[] GetBytes(byte[] data, int count)
    {
        if (data.Length <= count) return data;
        var bytes = new byte[count];
        Buffer.BlockCopy(data, 0, bytes, 0, count);
        return bytes;
    }

    private static byte[] InterleaveSessionKey(byte[] sBytes, SHA256 sha)
    {
        var first0Position = Array.IndexOf(sBytes, (byte)0);
        var startIndex1 = sBytes.Length - 1;
        var length = 4;
        if (first0Position != -1 && first0Position < (sBytes.Length - 4))
            length = sBytes.Length - first0Position;

        var part1 = new byte[length >> 1];
        var part2 = new byte[length >> 1];
        for (int i = 0, j = startIndex1, kk = startIndex1 - 1; i < part1.Length; i++, j -= 2, kk -= 2)
        {
            part1[i] = sBytes[j];
            part2[i] = sBytes[kk];
        }
        part1 = sha.ComputeHash(part1);
        part2 = sha.ComputeHash(part2);

        var key = new byte[sBytes.Length / 2];
        for (var i = 0; i < part1.Length && i * 2 + 1 < key.Length; i++)
        {
            key[i * 2] = part1[i];
            key[i * 2 + 1] = part2[i];
        }
        return key;
    }

    private static byte[] ComputeM1(byte[] I, byte[] salt, byte[] A, byte[] B, byte[] sessionKey, SHA256 sha)
    {
        var NHash = sha.ComputeHash(N);
        var gHash = sha.ComputeHash(gBytes);
        for (var i = 0; i < NHash.Length; i++) NHash[i] ^= gHash[i];
        return sha.ComputeHash(Combine(NHash, I, salt, A, B, sessionKey));
    }

    private static byte[] Combine(params byte[][] parts)
    {
        int total = 0;
        foreach (var p in parts) total += p.Length;
        var r = new byte[total];
        int off = 0;
        foreach (var p in parts) { Buffer.BlockCopy(p, 0, r, off, p.Length); off += p.Length; }
        return r;
    }

    private static string ToHex(byte[] data)
    {
        var sb = new StringBuilder(data.Length * 2);
        foreach (var b in data) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static byte[] FromHex(string hex)
    {
        var b = new byte[hex.Length / 2];
        for (int i = 0; i < b.Length; i++)
            b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return b;
    }
}
