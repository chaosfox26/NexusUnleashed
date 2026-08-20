using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NexusUnleashed.Network;
using NexusUnleashed.Cryptography;
using NexusUnleashed.Realm;

int pass = 0, fail = 0;
void Check(string n, bool ok, string d = "") { if (ok) { pass++; Console.WriteLine($"  PASS {n} {d}"); } else { fail++; Console.WriteLine($"  FAIL {n} {d}"); } }

const int port = 24099;
using var cts = new CancellationTokenSource();

var world = new GameServer("127.0.0.1", port, worldChannel: true);
WorldHandshake.Register(world);
var serverTask = world.ListenAsync(cts.Token);
await Task.Delay(300);
using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
await client.ConnectAsync("127.0.0.1", port);
Console.WriteLine("-- loopback client connected --");

async Task<(ushort Op, byte[] Payload)> ReadFrame()
{
    byte[] sizeBuf = await ReadExact(4);
    int size = sizeBuf[0] | (sizeBuf[1] << 8) | (sizeBuf[2] << 16) | (sizeBuf[3] << 24);
    byte[] rest = await ReadExact(size - 4);
    var frame = new byte[size];
    Array.Copy(sizeBuf, frame, 4);
    Array.Copy(rest, 0, frame, 4, rest.Length);
    return GamePacketFrame.Decode(frame);
}
async Task<byte[]> ReadExact(int n)
{
    var buf = new byte[n]; int got = 0;
    while (got < n)
    {
        int r = await client.ReceiveAsync(new ArraySegment<byte>(buf, got, n - got), SocketFlags.None);
        if (r == 0) throw new Exception("socket closed");
        got += r;
    }
    return buf;
}

var authCrypt = new PacketCrypt(PacketCrypt.AuthChannelKey);
var (op0, pay0) = await ReadFrame();
Check("hello frame is a 0x03DC container", op0 == WorldPacket.ServerContainer, $"(0x{op0:X4})");
var (helloOp, _) = WorldPacket.DecodeContainer(pay0, authCrypt);
Check("hello decrypts (auth key) to inner opcode 0x0003", helloOp == 0x0003, $"(0x{helloOp:X4})");

var clientAuth = new PacketCrypt(PacketCrypt.AuthChannelKey);
byte[] helloBody = new byte[41];byte[] clientFrame = WorldPacket.EncodeClient(0x058F, helloBody, clientAuth);
await client.SendAsync(clientFrame, SocketFlags.None);
Console.WriteLine("-- sent 0x058F; server should re-key + stream world entry --");

var worldCrypt = new PacketCrypt(WorldHandshake.DevWorldKey);
var (op1, pay1) = await ReadFrame();
Check("world frame is a 0x03DC container", op1 == WorldPacket.ServerContainer, $"(0x{op1:X4})");
var (wOp, wBody) = WorldPacket.DecodeContainer(pay1, worldCrypt);
Check("world message decrypts (WORLD key) to inner opcode 0x0981", wOp == 0x0981, $"(0x{wOp:X4})");
var full = new byte[wBody.Length + 2];
full[0] = 0x81; full[1] = 0x09; Array.Copy(wBody, 0, full, 2, wBody.Length);
var wi = ServerWorldInit.Parse(full);
Check("world-init carries the 251-id list, decrypted end-to-end over the socket",
    wi.Ids.Length == 251 && wi.Ids[0] == 1 && wi.Ids[250] == 251, $"({wi.Ids.Length} ids)");

cts.Cancel();
try { await serverTask; } catch { }

Console.WriteLine($"{pass} pass / {fail} fail");
return fail == 0 ? 0 : 1;
