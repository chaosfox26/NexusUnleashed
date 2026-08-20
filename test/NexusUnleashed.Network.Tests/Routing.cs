using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NexusUnleashed.Network;

public static class Routing
{
    public static async Task<int> RunAsync()
    {
        int pass = 0, fail = 0;
        void Check(string n, bool ok, string d = "") { if (ok) { pass++; Console.WriteLine($"  PASS {n} {d}"); } else { fail++; Console.WriteLine($"  FAIL {n} {d}"); } }

        Check("registry knows WorldHello 0x03DC", Opcodes.TryGet(0x03DC, out var w) && w.Name == "WorldHello");
        Check("registry knows AuthHello 0x0003", Opcodes.TryGet(0x0003, out _));
        Check("unknown opcode stable name", Opcodes.NameOf(0x7777) == "op_0x7777");

        var logs = new List<string>();
        var server = new GameServer("127.0.0.1", 27400);
        var router = new WorldMessageRouter(logs.Add);

        byte[]? received = null;
        var gotIt = new TaskCompletionSource();
        router.On(server, 0x7777, (s, payload) => { received = payload; gotIt.TrySetResult(); return Task.CompletedTask; });

        using var cts = new CancellationTokenSource();
        _ = server.ListenAsync(cts.Token);
        await Task.Delay(250);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", 27400);
        byte[] frame = GamePacketFrame.Encode(0x7777, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
        await client.GetStream().WriteAsync(frame);

        await Task.WhenAny(gotIt.Task, Task.Delay(3000));
        Check("handler received the frame", received != null);
        Check("payload intact", received != null && received.SequenceEqual(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }));
        Check("unpinned opcode flagged once", logs.Count(l => l.Contains("op_0x7777")) == 1, $"({logs.Count} logs)");

        cts.Cancel();
        Console.WriteLine($"{pass} pass / {fail} fail");
        return fail == 0 ? 0 : 1;
    }
}
