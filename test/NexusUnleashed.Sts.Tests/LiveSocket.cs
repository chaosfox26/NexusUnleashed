// In-process live-socket proof of the STS server: real TCP, full flow.
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NexusUnleashed.Sts;

public static class LiveSocket
{
    public static async Task<int> RunAsync()
    {
        int pass = 0, fail = 0;
        void Check(string name, bool ok)
        {
            if (ok) { pass++; Console.WriteLine($"  PASS {name}"); }
            else { fail++; Console.WriteLine($"  FAIL {name}"); }
        }

        TaskScheduler.UnobservedTaskException += (_, e) =>
            Console.WriteLine("UNOBSERVED: " + e.Exception);

        var server = new StsServer("127.0.0.1", 16600);
        AuthFlow.Register(server, new TestStore());
        using var cts = new CancellationTokenSource();
        Task listen = server.ListenAsync(cts.Token);
        await Task.Delay(300);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", 16600);
        NetworkStream ns = client.GetStream();
        ns.ReadTimeout = 5000;

        async Task<string> Roundtrip(string uri, int seq, string body = "")
        {
            byte[] b = Encoding.UTF8.GetBytes(body);
            byte[] head = Encoding.ASCII.GetBytes($"POST {uri} STS/1.0\r\nl:{b.Length}\r\ns:{seq}\r\n\r\n");
            await ns.WriteAsync(head); await ns.WriteAsync(b);
            byte[] buf = new byte[4096];
            using var readCts = new CancellationTokenSource(5000);
            int n;
            try { n = await ns.ReadAsync(buf, readCts.Token); }
            catch (OperationCanceledException) { return "<TIMEOUT>"; }
            return Encoding.UTF8.GetString(buf, 0, n);
        }

        string r1 = await Roundtrip("/Sts/Connect", 1);
        Check("Connect -> 200", r1.StartsWith("STS/1.0 200 OK"));
        if (r1 == "<TIMEOUT>") Console.WriteLine("  (no reply to Connect)");

        string r2 = await Roundtrip("/Auth/LoginStart", 2, "<Content>chara</Content>");
        Check("LoginStart -> 200 KeyData", r2.Contains("200 OK") && r2.Contains("KeyData"));

        string r3 = await Roundtrip("/Auth/RequestGameToken", 3);
        Check("RequestGameToken -> token", r3.Contains("200 OK") && r3.Contains("<Token>"));

        string r4 = await Roundtrip("/Bogus/Nothing", 4);
        Check("unknown route -> 400, connection survives", r4.Contains("400"));

        string r5 = await Roundtrip("/Sts/Ping", 5);
        Check("Ping after error still answered", r5.StartsWith("STS/1.0 200 OK"));

        cts.Cancel();
        Console.WriteLine($"{pass} pass / {fail} fail");
        return fail == 0 ? 0 : 1;
    }

    private sealed class TestStore : IAccountStore
    {
        public Task<(byte[] Salt, byte[] Verifier)?> GetSrpCredentialsAsync(string loginName)
            => Task.FromResult<(byte[], byte[])?>((new byte[32], Array.Empty<byte>()));
        public Task StoreGameTokenAsync(string loginName, Guid token) => Task.CompletedTask;
    }
}
