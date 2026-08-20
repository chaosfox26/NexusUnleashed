using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NexusUnleashed.Cryptography;
using NexusUnleashed.Sts;

public static class LiveSocket
{
    public static async Task<int> RunAsync()
    {
        int pass = 0, fail = 0;
        void Check(string name, bool ok, string d = "")
        { if (ok) { pass++; Console.WriteLine($"  PASS {name} {d}"); } else { fail++; Console.WriteLine($"  FAIL {name} {d}"); } }

        string login = "captain@nexusunleashed.test", password = "eldan secrets";
        byte[] saltBytes = Rng.GenerateRandomKey(16);
        string saltHex = string.Concat(saltBytes.Select(b => b.ToString("x2")));
        string verifierHex = SrpReferenceClient.ComputeVerifier(saltHex, login, password);
        var store = new TestStore(login, FromHex(saltHex), FromHex(verifierHex));

        var server = new StsServer("127.0.0.1", 16601);
        AuthFlow.Register(server, store);
        using var cts = new CancellationTokenSource();
        _ = server.ListenAsync(cts.Token);
        await Task.Delay(300);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", 16601);
        NetworkStream ns = client.GetStream();

        async Task<string> Rt(string uri, int seq, string body = "")
        {
            byte[] b = Encoding.UTF8.GetBytes(body);
            byte[] head = Encoding.ASCII.GetBytes($"POST {uri} STS/1.0\r\nl:{b.Length}\r\ns:{seq}\r\n\r\n");
            await ns.WriteAsync(head); await ns.WriteAsync(b);
            byte[] buf = new byte[8192];
            using var to = new CancellationTokenSource(5000);
            int n = await ns.ReadAsync(buf, to.Token);
            return Encoding.UTF8.GetString(buf, 0, n);
        }

        Check("Connect", (await Rt("/Sts/Connect", 1)).StartsWith("STS/1.0 200 OK"));

        string startReply = await Rt("/Auth/LoginStart", 2, $"<Content>{login}</Content>");
        Check("LoginStart -> 200 with salt+B", startReply.Contains("200 OK") && startReply.Contains("<B>"));
        byte[] B = FromHex(Field(startReply, "B"));
        Check("B is 128 bytes", B.Length == 128, $"({B.Length})");

        var cli = SrpReferenceClient.Respond(saltHex, login, password, B);
        string keyReply = await Rt("/Auth/KeyData", 3,
            $"<Content><A>{Hex(cli.PublicA)}</A><M1>{Hex(cli.ProofM1)}</M1></Content>");
        Check("KeyData -> 200 with M2 (login accepted!)", keyReply.Contains("200 OK") && keyReply.Contains("<M2>"));

        string tokReply = await Rt("/Auth/RequestGameToken", 4);
        Check("RequestGameToken -> token", tokReply.Contains("200 OK") && tokReply.Contains("<token>"));
        Check("token stored on account", store.LastToken != Guid.Empty);

        using (var c2 = new TcpClient())
        {
            await c2.ConnectAsync("127.0.0.1", 16601);
            var ns2 = c2.GetStream();
            async Task<string> Rt2(string uri, int seq, string body = "")
            {
                byte[] b = Encoding.UTF8.GetBytes(body);
                byte[] h = Encoding.ASCII.GetBytes($"POST {uri} STS/1.0\r\nl:{b.Length}\r\ns:{seq}\r\n\r\n");
                await ns2.WriteAsync(h); await ns2.WriteAsync(b);
                byte[] buf = new byte[8192]; using var to = new CancellationTokenSource(5000);
                int n = await ns2.ReadAsync(buf, to.Token); return Encoding.UTF8.GetString(buf, 0, n);
            }
            await Rt2("/Sts/Connect", 1);
            string sr = await Rt2("/Auth/LoginStart", 2, $"<Content>{login}</Content>");
            byte[] b2 = FromHex(Field(sr, "B"));
            var bad = SrpReferenceClient.Respond(saltHex, login, "WRONG", b2);
            string kr = await Rt2("/Auth/KeyData", 3, $"<Content><A>{Hex(bad.PublicA)}</A><M1>{Hex(bad.ProofM1)}</M1></Content>");
            Check("wrong password -> ERROR over the wire", kr.Contains("ERROR"));
        }

        cts.Cancel();
        Console.WriteLine($"{pass} pass / {fail} fail");
        return fail == 0 ? 0 : 1;
    }

    private static string Hex(byte[] b) => string.Concat(b.Select(x => x.ToString("x2")));
    private static byte[] FromHex(string h)
    { var b = new byte[h.Length / 2]; for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(h.Substring(i * 2, 2), 16); return b; }
    private static string Field(string xml, string tag)
    {
        string o = "<" + tag + ">", c = "</" + tag + ">";
        int i = xml.IndexOf(o, StringComparison.Ordinal); if (i < 0) return "";
        i += o.Length; int j = xml.IndexOf(c, i, StringComparison.Ordinal);
        return j < 0 ? "" : xml[i..j];
    }

    private sealed class TestStore : IAccountStore
    {
        private readonly string _login; private readonly byte[] _salt, _verifier;
        public Guid LastToken { get; private set; }
        public TestStore(string login, byte[] salt, byte[] verifier) { _login = login; _salt = salt; _verifier = verifier; }
        public Task<(byte[] Salt, byte[] Verifier)?> GetSrpCredentialsAsync(string loginName)
            => Task.FromResult(loginName == _login ? ((byte[], byte[])?)(_salt, _verifier) : null);
        public Task StoreGameTokenAsync(string loginName, Guid token) { LastToken = token; return Task.CompletedTask; }
    }
}
