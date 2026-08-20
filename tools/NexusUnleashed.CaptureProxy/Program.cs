using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

if (args.Length < 4)
{
    Console.WriteLine("usage: proxy <listenPort> <targetHost> <targetPort> <logFile>");
    return 2;
}
int listenPort = int.Parse(args[0]);
string targetHost = args[1];
int targetPort = int.Parse(args[2]);
string logPath = args[3];
bool raw = args.Length >= 5 && args[4].Equals("raw", StringComparison.OrdinalIgnoreCase);

using var log = new StreamWriter(logPath, append: true) { AutoFlush = true };
void Log(string line)
{
    string stamped = $"{DateTime.UtcNow:HH:mm:ss.fff} {line}";
    Console.WriteLine(stamped);
    lock (log) log.WriteLine(stamped);
}

Log($"# capture proxy: :{listenPort} -> {targetHost}:{targetPort}, log {logPath}");

var listener = new TcpListener(IPAddress.Loopback, listenPort);
for (int attempt = 1; ; attempt++)
{
    try { listener.Start(); break; }
    catch (SocketException)
    {
        if (attempt == 1) Log($"# port {listenPort} busy; waiting for it to free (retrying)...");
        await Task.Delay(1000);
    }
}
Log($"# listening on 127.0.0.1:{listenPort}");

while (true)
{
    TcpClient client = await listener.AcceptTcpClientAsync();
    _ = HandleAsync(client);
}

async Task HandleAsync(TcpClient client)
{
    int id = Environment.TickCount & 0xffff;
    Log($"# [{id}] client connected from {client.Client.RemoteEndPoint}");
    try
    {
        using var upstream = new TcpClient();
        await upstream.ConnectAsync(targetHost, targetPort);
        Log($"# [{id}] connected to oracle {targetHost}:{targetPort}");

        NetworkStream cs = client.GetStream(), us = upstream.GetStream();
        var c2s = Pump(cs, us, id, "C->S");
        var s2c = Pump(us, cs, id, "S->C");
        await Task.WhenAny(c2s, s2c);
        Log($"# [{id}] closed");
    }
    catch (Exception ex) { Log($"# [{id}] error: {ex.Message}"); }
    finally { client.Dispose(); }
}

async Task Pump(NetworkStream from, NetworkStream to, int id, string dir)
{
    var acc = new List<byte>();
    var buf = new byte[16384];
    try
    {
        while (true)
        {
            int n = await from.ReadAsync(buf);
            if (n == 0) break;
            await to.WriteAsync(buf.AsMemory(0, n));            if (raw)
            {
                DumpRaw(buf, n, id, dir);
                continue;
            }
            for (int i = 0; i < n; i++) acc.Add(buf[i]);
            DrainFrames(acc, id, dir);
        }
    }
    catch { }
}

void DumpRaw(byte[] buf, int n, int id, string dir)
{
    var hex = new StringBuilder(n * 2);
    var txt = new StringBuilder(n);
    for (int i = 0; i < n; i++)
    {
        hex.Append(buf[i].ToString("x2"));
        char c = (char)buf[i];
        txt.Append(c >= ' ' && c < 127 ? c : '.');
    }
    Log($"[{id}] {dir} {n}B text: {txt}");
    Log($"[{id}] {dir} {n}B  hex: {hex}");
}

void DrainFrames(List<byte> acc, int id, string dir)
{
    while (acc.Count >= 6)
    {
        uint size = (uint)(acc[0] | acc[1] << 8 | acc[2] << 16 | acc[3] << 24);
        if (size < 6 || size > 1_000_000) { acc.Clear(); return; }        if (acc.Count < size) return;
        ushort opcode = (ushort)(acc[4] | acc[5] << 8);
        int payloadLen = (int)size - 6;
        var sb = new StringBuilder();
        int preview = Math.Min(payloadLen, 16);
        for (int k = 0; k < preview; k++) sb.Append(acc[6 + k].ToString("x2"));
        Log($"[{id}] {dir} op=0x{opcode:X4} ({opcode}) len={payloadLen} {sb}");
        acc.RemoveRange(0, (int)size);
    }
}
