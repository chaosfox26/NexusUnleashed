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

Console.WriteLine($"{pass} pass / {fail} fail");
return fail == 0 ? 0 : 1;
