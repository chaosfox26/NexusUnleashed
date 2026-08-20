using System;
using System.Numerics;
using System.Security.Cryptography;

namespace NexusUnleashed.Cryptography;

public sealed class StsSrp
{
    private static readonly byte[] NB =
    {
        0xE3,0x06,0xEB,0xC0,0x2F,0x1D,0xC6,0x9F,0x5B,0x43,0x76,0x83,0xFE,0x38,0x51,0xFD,
        0x9A,0xAA,0x6E,0x97,0xF4,0xCB,0xD4,0x2F,0xC0,0x6C,0x72,0x05,0x3C,0xBC,0xED,0x68,
        0xEC,0x57,0x0E,0x66,0x66,0xF5,0x29,0xC5,0x85,0x18,0xCF,0x7B,0x29,0x9B,0x55,0x82,
        0x49,0x5D,0xB1,0x69,0xAD,0xF4,0x8E,0xCE,0xB6,0xD6,0x54,0x61,0xB4,0xD7,0xC7,0x5D,
        0xD1,0xDA,0x89,0x60,0x1D,0x5C,0x49,0x8E,0xE4,0x8B,0xB9,0x50,0xE2,0xD8,0xD5,0xE0,
        0xE0,0xC6,0x92,0xD6,0x13,0x48,0x3B,0x38,0xD3,0x81,0xEA,0x96,0x74,0xDF,0x74,0xD6,
        0x76,0x65,0x25,0x9C,0x4C,0x31,0xA2,0x9E,0x0B,0x3C,0xFF,0x75,0x87,0x61,0x72,0x60,
        0xE8,0xC5,0x8F,0xFA,0x0A,0xF8,0x33,0x9C,0xD6,0x8D,0xB3,0xAD,0xB9,0x0A,0xAF,0xEE
    };
    private static readonly byte[] gBytes = { 2, 0, 0, 0 };

    private readonly SHA256 _sha = SHA256.Create();
    private readonly BigInteger _N, _g, _k, _v;
    private readonly byte[] _salt, _I;
    private BigInteger _b;
    public byte[] B { get; private set; } = Array.Empty<byte>();    public string MatchedVariant { get; private set; } = "game-SRP (little-endian)";

    public StsSrp(byte[] salt, byte[] verifier, string username = "")
    {
        _N = LeToBig(NB);
        _g = new BigInteger(2);
        _k = LeToBig(ReverseUInt32(_sha.ComputeHash(Combine(NB, gBytes))));
        _salt = salt;
        _v = LeToBig(verifier);        _I = _sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(username));
    }

    public byte[] StartHandshake()
    {
        _b = LeToBig(Rng.GenerateRandomKey(0x20));
        BigInteger Bn = (_k * _v + BigInteger.ModPow(_g, _b, _N)) % _N;
        B = GetBytes(BigToLe(Bn), 0x80);
        return B;
    }

    public bool Verify(byte[] aLe, byte[] m1, out byte[] m2, out byte[] sessionKey)
    {
        m2 = Array.Empty<byte>(); sessionKey = Array.Empty<byte>();
        BigInteger A = LeToBig(aLe);
        if (A.IsZero || (A % _N).IsZero) return false;

        BigInteger u = LeToBig(ReverseUInt32(_sha.ComputeHash(Combine(aLe, B))));
        BigInteger S = BigInteger.ModPow(A * BigInteger.ModPow(_v, u, _N) % _N, _b, _N);
        byte[] K = InterleaveSessionKey(GetBytes(BigToLe(S), 0x80));

        byte[] expected = ComputeM1(aLe, K);
        if (!FixedEquals(expected, m1)) return false;
        m2 = _sha.ComputeHash(Combine(aLe, m1, K));        sessionKey = K;
        return true;
    }

    private byte[] ComputeM1(byte[] A, byte[] K)
    {
        byte[] nH = _sha.ComputeHash(NB), gH = _sha.ComputeHash(gBytes);
        for (int i = 0; i < nH.Length; i++) nH[i] ^= gH[i];
        return _sha.ComputeHash(Combine(nH, _I, _salt, A, B, K));    }

    private static BigInteger LeToBig(byte[] le) => new BigInteger(Combine(le, new byte[] { 0 }));
    private static byte[] BigToLe(BigInteger v) => v.ToByteArray();
    private static byte[] ReverseUInt32(byte[] d)
    {
        var r = new byte[d.Length];
        for (int i = 0; i < d.Length; i += 4) Buffer.BlockCopy(d, i, r, r.Length - (i + 4), 4);
        return r;
    }
    private static byte[] GetBytes(byte[] d, int count)
    {
        if (d.Length <= count) { var o = new byte[count]; Buffer.BlockCopy(d, 0, o, 0, d.Length); return o; }
        var b = new byte[count]; Buffer.BlockCopy(d, 0, b, 0, count); return b;
    }
    private byte[] InterleaveSessionKey(byte[] sBytes)
    {
        int first0 = Array.IndexOf(sBytes, (byte)0), start = sBytes.Length - 1, length = 4;
        if (first0 != -1 && first0 < sBytes.Length - 4) length = sBytes.Length - first0;
        var p1 = new byte[length >> 1]; var p2 = new byte[length >> 1];
        for (int i = 0, j = start, kk = start - 1; i < p1.Length; i++, j -= 2, kk -= 2) { p1[i] = sBytes[j]; p2[i] = sBytes[kk]; }
        p1 = _sha.ComputeHash(p1); p2 = _sha.ComputeHash(p2);
        var key = new byte[sBytes.Length / 2];
        for (int i = 0; i < p1.Length && i * 2 + 1 < key.Length; i++) { key[i * 2] = p1[i]; key[i * 2 + 1] = p2[i]; }
        return key;
    }
    private static byte[] Combine(params byte[][] parts)
    {
        int n = 0; foreach (var p in parts) n += p.Length;
        var r = new byte[n]; int o = 0;
        foreach (var p in parts) { Buffer.BlockCopy(p, 0, r, o, p.Length); o += p.Length; }
        return r;
    }
    private static bool FixedEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int d = 0; for (int i = 0; i < a.Length; i++) d |= a[i] ^ b[i]; return d == 0;
    }
}
