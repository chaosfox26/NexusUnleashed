// NexusUnleashed - clean-room authored. Public server-side driver over the
// WildStar SRP6a primitive (SRP6a.cs, MIT Arctium seed). The login server
// never sees a password: it holds only the account's salt + verifier (from
// authdb) and runs the standard SRP6a exchange:
//
//   1. StartHandshake()   -> server sends (salt, B) to the client
//   2. client -> (A, M1)
//   3. Verify(A, M1)      -> checks M1, returns (M2, sessionKey) or failure
//
// The N modulus, k, and the M1/M2 hash construction are the WildStar-specific
// forms carried by the primitive. This wrapper adds the clean public surface
// and the verification decision; it copies no server's code.
using System;

namespace NexusUnleashed.Cryptography;

public sealed class SrpServerResult
{
    public bool Success { get; init; }
    public byte[] ServerProof { get; init; } = Array.Empty<byte>();   // M2
    public byte[] SessionKey { get; init; } = Array.Empty<byte>();
}

public sealed class SrpServer : IDisposable
{
    private readonly SRP6a _srp;
    private bool _started;

    /// <param name="saltHex">SRP salt, hex (authdb column `s`).</param>
    /// <param name="accountName">login/email; folds into I = SHA256(name).</param>
    /// <param name="verifierHex">SRP verifier, hex (authdb column `v`).</param>
    public SrpServer(string saltHex, string accountName, string verifierHex)
        => _srp = new SRP6a(saltHex, accountName, verifierHex);

    /// <summary>As above, from raw salt/verifier bytes (what IAccountStore yields).</summary>
    public SrpServer(byte[] salt, string accountName, byte[] verifier)
        : this(ToHex(salt), accountName, ToHex(verifier)) { }

    private static string ToHex(byte[] b)
    {
        var c = new char[b.Length * 2];
        const string h = "0123456789abcdef";
        for (int i = 0; i < b.Length; i++) { c[i * 2] = h[b[i] >> 4]; c[i * 2 + 1] = h[b[i] & 0xF]; }
        return new string(c);
    }

    /// <summary>Server step 1: compute B. Returns the (salt, B) the client needs.</summary>
    public (byte[] Salt, byte[] B) StartHandshake()
    {
        _srp.CalculateB();
        _started = true;
        return (_srp.S, _srp.B);
    }

    /// <summary>
    /// Server step 2: given the client's public A and proof M1, derive the
    /// session key, verify M1, and return M2 on success.
    /// </summary>
    public SrpServerResult Verify(byte[] clientPublicA, byte[] clientProofM1)
    {
        if (!_started) throw new InvalidOperationException("StartHandshake() must run first.");

        // A == 0 (mod N) is rejected inside CalculateU (the SRP safety check).
        if (!_srp.CalculateU(clientPublicA))
            return new SrpServerResult { Success = false };

        _srp.CalculateClientM(clientPublicA);
        if (!FixedTimeEquals(_srp.ClientM, clientProofM1))
            return new SrpServerResult { Success = false };

        _srp.CalculateServerM(clientProofM1, clientPublicA);
        return new SrpServerResult
        {
            Success = true,
            ServerProof = _srp.ServerM,
            SessionKey = _srp.SessionKey,
        };
    }

    private static bool FixedTimeEquals(byte[] a, byte[] b)
    {
        if (a is null || b is null || a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    public void Dispose() => _srp.Dispose();
}
