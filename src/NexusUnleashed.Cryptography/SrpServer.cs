using System;

namespace NexusUnleashed.Cryptography;

public sealed class SrpServerResult
{
    public bool Success { get; init; }
    public byte[] ServerProof { get; init; } = Array.Empty<byte>();    public byte[] SessionKey { get; init; } = Array.Empty<byte>();
}

public sealed class SrpServer : IDisposable
{
    private readonly SRP6a _srp;
    private bool _started;

    public SrpServer(string saltHex, string accountName, string verifierHex)
        => _srp = new SRP6a(saltHex, accountName, verifierHex);

    public SrpServer(byte[] salt, string accountName, byte[] verifier)
        : this(ToHex(salt), accountName, ToHex(verifier)) { }

    private static string ToHex(byte[] b)
    {
        var c = new char[b.Length * 2];
        const string h = "0123456789abcdef";
        for (int i = 0; i < b.Length; i++) { c[i * 2] = h[b[i] >> 4]; c[i * 2 + 1] = h[b[i] & 0xF]; }
        return new string(c);
    }

    public (byte[] Salt, byte[] B) StartHandshake()
    {
        _srp.CalculateB();
        _started = true;
        return (_srp.S, _srp.B);
    }

    public SrpServerResult Verify(byte[] clientPublicA, byte[] clientProofM1)
    {
        if (!_started) throw new InvalidOperationException("StartHandshake() must run first.");

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
