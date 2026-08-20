using System;
using System.Globalization;
using System.Threading.Tasks;
using NexusUnleashed.Network;

namespace NexusUnleashed.Realm;

public static class AuthHandshake
{
    private const ushort ServerHello = 0x0003;

    private const string HelloBodyHex =
        "aa3e0000010000001500000000000000000000000000000000000b14332f0100000000000000000000000000000000";

    public static void Register(GameServer auth)
    {
        auth.OnConnected = async session =>
        {
            Log.Info($"auth: client connected {session.RemoteAddress} — sending clear 0x0003 hello");
            await session.SendGameMessageAsync(ServerHello, Hex(HelloBodyHex));        };

        auth.OnUnhandled = (s, opcode, body) =>
            Log.Info($"auth: <- op=0x{opcode:X4} ({body.Length}B) {Preview(body)}");
        Log.Info("auth: handshake registered (clear 0x0003 on connect; all client opcodes logged).");
    }

    private static byte[] Hex(string h)
    {
        var b = new byte[h.Length / 2];
        for (int i = 0; i < b.Length; i++)
            b[i] = byte.Parse(h.Substring(i * 2, 2), NumberStyles.HexNumber);
        return b;
    }

    private static string Preview(byte[] b)
    {
        int n = Math.Min(24, b.Length);
        var sb = new System.Text.StringBuilder(n * 2 + 3);
        for (int i = 0; i < n; i++) sb.Append(b[i].ToString("x2"));
        if (b.Length > n) sb.Append('…');
        return sb.ToString();
    }
}
