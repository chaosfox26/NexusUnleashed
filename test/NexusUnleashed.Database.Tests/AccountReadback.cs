// Read-only integration proof: the DB account store reads a real SRP credential
// out of the live authdb at the correct byte widths. Connection string + a
// known account email are passed on argv so no secret is baked into the repo.
using System;
using System.Threading.Tasks;
using NexusUnleashed.Database;

if (args.Length < 2)
{
    Console.WriteLine("SKIP (no connection string / email provided) - unit build only");
    return 0;
}
string conn = args[0], email = args[1];
var store = new DbAccountStore(conn);

int pass = 0, fail = 0;
void Check(string name, bool ok, string d = "")
{ if (ok) { pass++; Console.WriteLine($"  PASS {name} {d}"); } else { fail++; Console.WriteLine($"  FAIL {name} {d}"); } }

var creds = await store.GetSrpCredentialsAsync(email);
Check("known account found", creds != null);
if (creds != null)
{
    Check("salt = 16 bytes", creds.Value.Salt.Length == 16, $"({creds.Value.Salt.Length})");
    Check("verifier = 128 bytes", creds.Value.Verifier.Length == 128, $"({creds.Value.Verifier.Length})");
    Check("salt not all-zero", Array.Exists(creds.Value.Salt, b => b != 0));
}
var missing = await store.GetSrpCredentialsAsync("no-such-account@nowhere.invalid");
Check("unknown account -> null", missing == null);

Console.WriteLine($"{pass} pass / {fail} fail");
return fail == 0 ? 0 : 1;
