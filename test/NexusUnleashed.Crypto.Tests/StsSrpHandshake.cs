// Standard SRP-6a (STS channel) self-consistency proof: a reference standard
// client — mirroring StsSrp's big-endian, RFC-5054-style math — registers,
// runs the handshake against StsSrp, and both sides must derive the SAME
// session key with matching M1/M2. Then wrong password / A=0 must fail.
//
// This proves StsSrp is internally correct (no implementation bug). Whether the
// STS's exact hash + M1 layout match the STOCK CLIENT is decided LIVE against
// the client's own M1 (ground truth) — this test cannot and does not claim that.
using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using NexusUnleashed.Cryptography;

static class StsSrpChecks
{
public static int Run()
{
int pass = 0, fail = 0;
void Check(string name, bool ok, string d = "")
{ if (ok) { pass++; Console.WriteLine($"  PASS {name} {d}"); } else { fail++; Console.WriteLine($"  FAIL {name} {d}"); } }

// --- reference standard SRP-6a client (test-only) ---------------------------
byte[] NB = {
    0xE3,0x06,0xEB,0xC0,0x2F,0x1D,0xC6,0x9F,0x5B,0x43,0x76,0x83,0xFE,0x38,0x51,0xFD,
    0x9A,0xAA,0x6E,0x97,0xF4,0xCB,0xD4,0x2F,0xC0,0x6C,0x72,0x05,0x3C,0xBC,0xED,0x68,
    0xEC,0x57,0x0E,0x66,0x66,0xF5,0x29,0xC5,0x85,0x18,0xCF,0x7B,0x29,0x9B,0x55,0x82,
    0x49,0x5D,0xB1,0x69,0xAD,0xF4,0x8E,0xCE,0xB6,0xD6,0x54,0x61,0xB4,0xD7,0xC7,0x5D,
    0xD1,0xDA,0x89,0x60,0x1D,0x5C,0x49,0x8E,0xE4,0x8B,0xB9,0x50,0xE2,0xD8,0xD5,0xE0,
    0xE0,0xC6,0x92,0xD6,0x13,0x48,0x3B,0x38,0xD3,0x81,0xEA,0x96,0x74,0xDF,0x74,0xD6,
    0x76,0x65,0x25,0x9C,0x4C,0x31,0xA2,0x9E,0x0B,0x3C,0xFF,0x75,0x87,0x61,0x72,0x60,
    0xE8,0xC5,0x8F,0xFA,0x0A,0xF8,0x33,0x9C,0xD6,0x8D,0xB3,0xAD,0xB9,0x0A,0xAF,0xEE };
int NLEN = NB.Length;
BigInteger N = new BigInteger(NB, true, true), g = 2;
BigInteger FromBE(byte[] b) => new BigInteger(b, true, true);
byte[] ToBE(BigInteger v) => v.ToByteArray(true, true);
byte[] Pad(byte[] be) { if (be.Length == NLEN) return be; var o = new byte[NLEN];
    if (be.Length > NLEN) Array.Copy(be, be.Length - NLEN, o, 0, NLEN);
    else Array.Copy(be, 0, o, NLEN - be.Length, be.Length); return o; }
byte[] H(params byte[][] ps) { using var h = SHA256.Create();
    return h.ComputeHash(ps.SelectMany(x => x).ToArray()); }
BigInteger k = FromBE(H(NB, Pad(ToBE(g))));

string user = "player@nexusunleashed.test", pw = "correct horse battery staple";
byte[] salt = Rng.GenerateRandomKey(16);
// x = H(salt | H(user | ":" | pw)); v = g^x mod N
byte[] ipw = System.Text.Encoding.UTF8.GetBytes(user + ":" + pw);
BigInteger x = FromBE(H(salt, H(ipw)));
BigInteger v = BigInteger.ModPow(g, x, N);
// The DB stores the verifier as .NET BigInteger.ToByteArray() = little-endian
// (with a trailing sign byte); StsSrp reads it that way.
byte[] verifier = v.ToByteArray(isUnsigned: true, isBigEndian: false);

// server B
var srp = new StsSrp(salt, verifier);
byte[] B = srp.StartHandshake();
Check("server B is |N| bytes", B.Length == NLEN, $"({B.Length})");
BigInteger Bn = FromBE(B);
Check("B in (0, N)", Bn > 0 && Bn < N);

// client A, u, S, K, M1
BigInteger a = FromBE(Rng.GenerateRandomKey(32));
BigInteger A = BigInteger.ModPow(g, a, N);
byte[] Apad = Pad(ToBE(A)), Bpad = Pad(ToBE(Bn));
BigInteger u = FromBE(H(Apad, Bpad));
BigInteger Sc = BigInteger.ModPow((Bn - k * BigInteger.ModPow(g, x, N) % N + N * 4) % N, a + u * x, N);
byte[] Kc = H(Pad(ToBE(Sc)));
byte[] hN = H(NB), hg = H(Pad(ToBE(g)));
byte[] hx = new byte[hN.Length]; for (int i = 0; i < hN.Length; i++) hx[i] = (byte)(hN[i] ^ hg[i]);
byte[] M1 = H(hx, salt, Apad, Bpad, Kc);

bool ok = srp.Verify(Apad, M1, out byte[] M2, out byte[] Ks);
Check("server accepts correct client", ok);
Check("session keys AGREE (mutual auth)", ok && Ks.SequenceEqual(Kc), ok ? $"({Ks.Length}B)" : "");
Check("server returned M2 = H(A|M1|K)", ok && M2.SequenceEqual(H(Apad, M1, Kc)));

// wrong password must fail
{
    BigInteger xw = FromBE(H(salt, H(System.Text.Encoding.UTF8.GetBytes(user + ":wrong"))));
    BigInteger Sw = BigInteger.ModPow((Bn - k * BigInteger.ModPow(g, xw, N) % N + N * 4) % N, a + u * xw, N);
    byte[] M1w = H(hx, salt, Apad, Bpad, H(Pad(ToBE(Sw))));
    Check("wrong password rejected", !srp.Verify(Apad, M1w, out _, out _));
}
// A ≡ 0 mod N must abort
Check("A=0 rejected", !srp.Verify(Pad(ToBE(N)), M1, out _, out _));

Console.WriteLine($"\nSTS-SRP self-consistency: {pass} passed, {fail} failed");
return fail;
}
}
