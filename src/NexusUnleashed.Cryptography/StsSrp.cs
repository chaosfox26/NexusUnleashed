// NexusUnleashed — clean-room authored. STANDARD SRP-6a server for the STS
// (login) channel. This is a DIFFERENT variant from the game-channel SRP6a:
//
//   game SRP6a  : Carbine's own — ReverseUInt32 on every hash, little-endian
//                 BigInteger, block-reversed byte output. (SHA-256.)
//   STS SRP     : the stock client's Services/Srp/Srp.cpp — plain RFC-5054-style
//                 SRP-6a over OpenSSL bignums: big-endian BN_bin2bn values, the
//                 client validates B < N as a big-endian bignum.
//
// Provenance: the SRP FLOW and byte order are FACTS reverse-engineered from the
// stock client's own StsConnLib64.MT.dll (the LoginStart reply parser at
// 0x18002d4e0: reads [u32 LE saltLen][salt][u32 LE BLen][B] and checks B<N
// big-endian). The math is textbook SRP-6a, authored fresh here. The group
// (N, g=2) is the WildStar SRP prime already carried by the game SRP6a (MIT
// Arctium seed, ledgered). Owes the forbidden tree nothing.
//
// The hash is a single swappable parameter: SHA-256 by default (matches the
// game channel and modern practice); a one-line switch to SHA-1 if the client's
// M1 shows the STS uses OpenSSL's historical SHA-1. Which one is correct is
// decided by the client's OWN M1 (ground truth), never guessed.
using System;
using System.Numerics;
using System.Security.Cryptography;

namespace NexusUnleashed.Cryptography;

/// <summary>Standard SRP-6a server, big-endian, for the STS login channel.</summary>
public sealed class StsSrp
{
    // WildStar SRP group (1024-bit N, g = 2), big-endian. Same prime the game
    // SRP6a carries; here it is used the STANDARD way (big-endian, no reversal).
    private static readonly byte[] NBytes =
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

    /// <summary>Hash used for k, x, u, M1, M2, K. STS candidate = SHA-256; switch
    /// to SHA-1 if the client's M1 proves OpenSSL's historical choice.</summary>
    public enum HashKind { Sha256, Sha1 }

    private readonly HashKind _hash;
    private readonly int _nLen;          // fixed field width for PAD() = |N| bytes
    private readonly BigInteger _N, _g, _k;
    private readonly byte[] _salt, _vBytes, _userBytes;
    private readonly BigInteger _v;

    private BigInteger _b, _B;
    public byte[] B { get; private set; } = Array.Empty<byte>();  // big-endian, |N| wide

    /// <summary>Which M1-formula variant matched the client's proof (diagnostics).</summary>
    public string MatchedVariant { get; private set; } = "";

    /// <summary>Server secret b (big-endian) — diagnostics only, lets a failed
    /// proof be solved offline against the client's real M1 without a re-login.</summary>
    public byte[] SecretB => ToBE(_b);
    public byte[] Verifier => _vBytes;
    public byte[] Salt => _salt;

    public string KLabel { get; private set; } = "";

    public StsSrp(byte[] salt, byte[] verifier, string username = "", int kMode = 0, HashKind hash = HashKind.Sha256)
    {
        _hash = hash;
        _N = FromBE(NBytes);
        _nLen = NBytes.Length;
        _g = new BigInteger(2);
        _salt = salt;
        _vBytes = verifier;
        _userBytes = System.Text.Encoding.UTF8.GetBytes(username);
        // The verifier was written by the .NET launcher's BigInteger.ToByteArray()
        // = LITTLE-ENDIAN with a trailing 0x00 sign byte (the DB value is 129 bytes
        // and >N when misread big-endian). Read it little-endian, unsigned.
        _v = new BigInteger(verifier, isUnsigned: true, isBigEndian: false);
        // The SRP multiplier k. RFC-5054 H(N|PAD(g)) did not reproduce the
        // client's shared secret, so k is one of the common variants below; the
        // right one is identified when the client's own M1 verifies.
        (_k, KLabel) = kMode switch
        {
            1 => (new BigInteger(3), "k=3"),
            2 => (BigInteger.One, "k=1(SRP3: B=v+g^b)"),
            3 => (FromBE(H(NBytes, ToBE(_g))), "k=H(N|g-minimal)"),
            4 => (FromBE(H(Pad(ToBE(_g)), NBytes)), "k=H(PAD(g)|N)"),
            5 => (FromBE(H(NBytes)), "k=H(N)"),
            6 => (FromBE(H(ToBE(_g), NBytes)), "k=H(g-min|N)"),
            _ => (FromBE(H(NBytes, Pad(ToBE(_g)))), "k=H(N|PAD(g)) RFC5054"),
        };
    }

    /// <summary>Number of distinct k-modes to rotate through across retries.</summary>
    public const int KModeCount = 7;

    /// <summary>Pick b and compute B = (k*v + g^b) mod N. Returns B big-endian.</summary>
    public byte[] StartHandshake()
    {
        _b = FromBE(Rng.GenerateRandomKey(32));            // 256-bit secret
        _B = (_k * _v + BigInteger.ModPow(_g, _b, _N)) % _N;
        B = Pad(ToBE(_B));
        return B;
    }

    /// <summary>
    /// Verify the client's proof. A and M1 are big-endian (as sent in KeyData).
    /// On success returns the server proof M2 (big-endian) and the session key K.
    /// </summary>
    public bool Verify(byte[] aBE, byte[] m1, out byte[] m2, out byte[] sessionKey)
    {
        m2 = Array.Empty<byte>(); sessionKey = Array.Empty<byte>();
        MatchedVariant = "";
        BigInteger A = FromBE(aBE);
        if (A % _N == 0) return false;                      // A mod N == 0 -> abort

        byte[] Apad = Pad(ToBE(A)), Bpad = Pad(ToBE(_B));

        // Solve NCSoft's exact SRP recipe against the client's OWN M1 (ground
        // truth): SHA-256 (M1 is 32B on the wire); k comes from this session's
        // rotated mode; only u / K / M1 layout remain. Accept the combination
        // that reproduces the client's M1 — the proof is the oracle, not a guess.
        byte[] hN = H(NBytes), hg = H(Pad(ToBE(_g)));
        byte[] hNxorG = new byte[hN.Length];
        for (int i = 0; i < hN.Length; i++) hNxorG[i] = (byte)(hN[i] ^ hg[i]);
        byte[] hI = H(_userBytes);
        byte[] saltHexU = System.Text.Encoding.ASCII.GetBytes(Convert.ToHexString(_salt));
        byte[] saltHexL = System.Text.Encoding.ASCII.GetBytes(Convert.ToHexString(_salt).ToLowerInvariant());

        var uVariants = new (string tag, BigInteger u)[]
        {
            ("u=H(A|B)",     FromBE(H(Apad, Bpad))),
            ("u=H(B)",       FromBE(H(Bpad))),
            ("u=H(A|B)[:4]", FromBE(H(Apad, Bpad)[..4])),
        };
        foreach (var (utag, u) in uVariants)
        {
            BigInteger S = BigInteger.ModPow(A * BigInteger.ModPow(_v, u, _N) % _N, _b, _N);
            byte[] Spad = Pad(ToBE(S)), Smin = ToBE(S);
            var kVariants = new (string tag, byte[] K)[]
            {
                ("K=H(padS)",      H(Spad)),
                ("K=H(minS)",      H(Smin)),
                ("K=interleave",   Interleave(Smin)),
                ("K=padS",         Spad),
            };
            foreach (var (ktag, K) in kVariants)
            foreach (var (stag, salt) in new[] { ("s", _salt), ("sHexU", saltHexU), ("sHexL", saltHexL) })
            {
                var m1Variants = new (string tag, byte[] m1)[]
                {
                    ("H(hNg|hI|s|A|B|K)", H(hNxorG, hI, salt, Apad, Bpad, K)),
                    ("H(hNg|s|A|B|K)",    H(hNxorG, salt, Apad, Bpad, K)),
                    ("H(A|B|K)",          H(Apad, Bpad, K)),
                    ("H(A|B|S)",          H(Apad, Bpad, Spad)),
                    ("H(s|A|B|K)",        H(salt, Apad, Bpad, K)),
                    ("H(hI|s|A|B|K)",     H(hI, salt, Apad, Bpad, K)),
                };
                foreach (var (mtag, cand) in m1Variants)
                {
                    if (FixedEquals(cand, m1))
                    {
                        MatchedVariant = $"{KLabel} ; {utag} ; {ktag} ; salt={stag} ; M1={mtag}";
                        m2 = H(Apad, m1, K);                // M2 = H(A | M1 | K)
                        sessionKey = K;
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>RFC 2945 SHA_Interleave of the session secret S.</summary>
    private byte[] Interleave(byte[] s)
    {
        int i = 0; while (i < s.Length && s[i] == 0) i++;   // strip leading zeros
        byte[] t = s[i..];
        if ((t.Length & 1) == 1) t = t[1..];                // even length
        int half = t.Length / 2;
        byte[] e = new byte[half], o = new byte[half];
        for (int j = 0; j < half; j++) { e[j] = t[2 * j]; o[j] = t[2 * j + 1]; }
        byte[] he = H(e), ho = H(o);
        byte[] outp = new byte[he.Length + ho.Length];
        for (int j = 0; j < he.Length; j++) { outp[2 * j] = he[j]; outp[2 * j + 1] = ho[j]; }
        return outp;
    }

    // ---- hashing / big-endian helpers ------------------------------------

    private byte[] H(params byte[][] parts)
    {
        using HashAlgorithm h = _hash == HashKind.Sha1 ? SHA1.Create() : SHA256.Create();
        int n = 0; foreach (var p in parts) n += p.Length;
        var buf = new byte[n]; int o = 0;
        foreach (var p in parts) { p.CopyTo(buf, o); o += p.Length; }
        return h.ComputeHash(buf);
    }

    /// <summary>Big-endian unsigned bytes -> BigInteger.</summary>
    private static BigInteger FromBE(byte[] be)
        => new BigInteger(be, isUnsigned: true, isBigEndian: true);

    /// <summary>BigInteger -> big-endian unsigned bytes (minimal length).</summary>
    private static byte[] ToBE(BigInteger v)
        => v.ToByteArray(isUnsigned: true, isBigEndian: true);

    /// <summary>Left-pad big-endian bytes to |N| width.</summary>
    private byte[] Pad(byte[] be)
    {
        if (be.Length == _nLen) return be;
        if (be.Length > _nLen) { var t = new byte[_nLen]; Array.Copy(be, be.Length - _nLen, t, 0, _nLen); return t; }
        var o = new byte[_nLen]; Array.Copy(be, 0, o, _nLen - be.Length, be.Length); return o;
    }

    private static bool FixedEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int d = 0; for (int i = 0; i < a.Length; i++) d |= a[i] ^ b[i];
        return d == 0;
    }
}
