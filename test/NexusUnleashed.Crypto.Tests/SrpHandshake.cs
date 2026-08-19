// Full SRP6a login proof: register (compute verifier) -> server B -> client
// (A, M1) -> server verifies -> both derive the SAME session key, and M1/M2
// prove mutual authentication. Then the failure cases must actually fail.
using System;
using System.Linq;
using NexusUnleashed.Cryptography;

int pass = 0, fail = 0;
void Check(string name, bool ok, string d = "")
{ if (ok) { pass++; Console.WriteLine($"  PASS {name} {d}"); } else { fail++; Console.WriteLine($"  FAIL {name} {d}"); } }

string name = "player@nexusunleashed.test";
string password = "correct horse battery staple";
byte[] saltBytes = Rng.GenerateRandomKey(16);
string saltHex = string.Concat(saltBytes.Select(b => b.ToString("x2")));

// registration side: verifier from the password
string verifierHex = SrpReferenceClient.ComputeVerifier(saltHex, name, password);
Check("verifier computed", verifierHex.Length > 0, $"({verifierHex.Length/2} bytes)");

// happy path
using (var server = new SrpServer(saltHex, name, verifierHex))
{
    var (salt, B) = server.StartHandshake();
    Check("server B is 128 bytes", B.Length == 128, $"({B.Length})");
    Check("server echoes salt", salt.SequenceEqual(saltBytes));

    var client = SrpReferenceClient.Respond(saltHex, name, password, B);
    var result = server.Verify(client.PublicA, client.ProofM1);

    Check("server accepts correct client", result.Success);
    Check("session keys AGREE (mutual auth)", result.Success && result.SessionKey.SequenceEqual(client.SessionKey),
        result.Success ? $"({result.SessionKey.Length}B)" : "");
    Check("server returned M2", result.Success && result.ServerProof.Length == 32);
}

// wrong password must be rejected
using (var server = new SrpServer(saltHex, name, verifierHex))
{
    var (_, B) = server.StartHandshake();
    var badClient = SrpReferenceClient.Respond(saltHex, name, "wrong password", B);
    Check("wrong password REJECTED", !server.Verify(badClient.PublicA, badClient.ProofM1).Success);
}

// A == 0 must be rejected (SRP safety)
using (var server = new SrpServer(saltHex, name, verifierHex))
{
    server.StartHandshake();
    Check("A=0 REJECTED", !server.Verify(new byte[128], new byte[32]).Success);
}

// tampered M1 must be rejected
using (var server = new SrpServer(saltHex, name, verifierHex))
{
    var (_, B) = server.StartHandshake();
    var c = SrpReferenceClient.Respond(saltHex, name, password, B);
    var tampered = (byte[])c.ProofM1.Clone(); tampered[0] ^= 0xFF;
    Check("tampered M1 REJECTED", !server.Verify(c.PublicA, tampered).Success);
}


// --- PacketCrypt (game channel) ---
Console.WriteLine("-- packet crypt --");
{
    // server encrypt -> client decrypt recovers the plaintext (same static seed).
    ulong key = 0x0123456789ABCDEFul;
    byte[] msg = System.Text.Encoding.ASCII.GetBytes("NexusUnleashed world packet payload, arbitrary length 12345");
    byte[] ct = new PacketCrypt(key).Encrypt(msg, msg.Length);
    Check("ciphertext differs from plaintext", !ct.AsSpan().SequenceEqual(msg));
    byte[] pt = new PacketCrypt(key).Decrypt(ct, ct.Length);
    Check("decrypt(encrypt(x)) == x", pt.AsSpan().SequenceEqual(msg));
}


// --- Carbine packet cipher vs REAL captured keystream (gate-closing proof) ---
Console.WriteLine("-- packet cipher vs real wire --");
{
    // seed = static build key (observed at runtime, confirmed by the live tap)
    ulong seed = 0xD283F5B34A8DC685ul;
    // keystream captured from the oracle's wire (real cipher output, position 0)
    byte[] real = Convert.FromHexString("cf0c0e97c85f02238ce856b6f60d9b1d84466f01e710339191612a4284105ff8");
    var pc = new PacketCrypt(seed);
    byte[] ks = pc.Encrypt(new byte[real.Length], real.Length);
    Check("clean cipher reproduces the REAL captured keystream (gate closed)", ks.AsSpan().SequenceEqual(real));

    // round trip: server encrypt -> client decrypt recovers the plaintext
    var srv = new PacketCrypt(seed);
    var cli = new PacketCrypt(seed);
    byte[] msg = System.Text.Encoding.ASCII.GetBytes("hello from NexusUnleashed");
    byte[] ct = srv.Encrypt(msg, msg.Length);
    byte[] pt = cli.Decrypt(ct, ct.Length);
    Check("server-encrypt -> client-decrypt round trip", pt.AsSpan().SequenceEqual(msg));
}

Console.WriteLine($"{pass} pass / {fail} fail");

Console.WriteLine("\n== STS standard SRP-6a (login channel) ==");
int stsFail = StsSrpChecks.Run();

return (fail == 0 && stsFail == 0) ? 0 : 1;
