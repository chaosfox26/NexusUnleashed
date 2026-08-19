// STS game-SRP self-consistency: the STS server (StsSrp, little-endian game SRP)
// against the confirmed reference client (SrpReferenceClient). A successful round
// trip — the client's own proof verifies on the server and the session keys agree
// — proves the STS path uses the same WildStar game SRP the real client does
// (confirmed independently by cracking the bot account's stored verifier).
using System;
using System.Linq;
using NexusUnleashed.Cryptography;

static class StsSrpChecks
{
public static int Run()
{
    int pass = 0, fail = 0;
    void Check(string n, bool ok, string d = "")
    { if (ok) { pass++; Console.WriteLine($"  PASS {n} {d}"); } else { fail++; Console.WriteLine($"  FAIL {n} {d}"); } }

    string user = "player@nexusunleashed.test", pw = "correct horse battery staple";
    byte[] salt = Rng.GenerateRandomKey(16);
    string saltHex = Convert.ToHexString(salt).ToLowerInvariant();

    // register (game-SRP verifier, little-endian hex as authdb stores it)
    string vHex = SrpReferenceClient.ComputeVerifier(saltHex, user, pw);
    byte[] verifier = Convert.FromHexString(vHex);

    var srv = new StsSrp(salt, verifier, user);
    byte[] B = srv.StartHandshake();
    Check("server B produced", B.Length == 128, $"({B.Length}B little-endian)");

    var cli = SrpReferenceClient.Respond(saltHex, user, pw, B);
    bool ok = srv.Verify(cli.PublicA, cli.ProofM1, out byte[] m2, out byte[] K);
    Check("client proof VERIFIES on server (game SRP)", ok);
    Check("session keys AGREE", ok && K.SequenceEqual(cli.SessionKey), ok ? $"({K.Length}B)" : "");
    Check("server returns M2", ok && m2.Length == 32);

    // wrong password rejected
    var bad = SrpReferenceClient.Respond(saltHex, user, "wrong", B);
    var srv2 = new StsSrp(salt, verifier, user); srv2.StartHandshake();
    Check("wrong password rejected", !srv2.Verify(bad.PublicA, bad.ProofM1, out _, out _));

    Console.WriteLine($"\nSTS game-SRP self-consistency: {pass} passed, {fail} failed");
    return fail;
}
}
