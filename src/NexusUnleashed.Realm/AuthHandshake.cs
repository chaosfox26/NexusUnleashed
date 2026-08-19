// NexusUnleashed - clean-room authored. The auth channel (port 23115) handshake.
// The auth channel is CLEAR (not the world channel's encrypted container): the
// oracle capture (spec/protocol/frame.md) shows it opens with a clear 0x0003
// hello (size=53, opcode 0x0003, 47-byte body). This stub accepts the client's
// auth connection, sends that hello, and LOGS whatever the client sends back, so
// a real login attempt reveals the auth-channel messages to pin next. Bodies are
// captured templates until decoded against the client-as-oracle loop.
using System;
using System.Globalization;
using System.Threading.Tasks;
using NexusUnleashed.Network;

namespace NexusUnleashed.Realm;

/// <summary>
/// Wires the auth <see cref="GameServer"/> (clear channel): the on-connect 0x0003
/// hello and a catch-log for the client's replies. Purely for the capture stage.
/// </summary>
public static class AuthHandshake
{
    private const ushort ServerHello = 0x0003;

    // UNPINNED template: the 0x0003 hello body (47 bytes after the opcode), reused
    // from our own capture. The auth-channel hello may differ from the world one;
    // a login attempt against this reveals the truth.
    private const string HelloBodyHex =
        "aa3e0000010000001500000000000000000000000000000000000b14332f0100000000000000000000000000000000";

    public static void Register(GameServer auth)
    {
        auth.OnConnected = async session =>
        {
            Log.Info($"auth: client connected {session.RemoteAddress} — sending clear 0x0003 hello");
            await session.SendGameMessageAsync(ServerHello, Hex(HelloBodyHex));  // Crypt==null -> clear frame
        };

        // Log EVERY opcode the client sends on the auth channel — this is how the
        // auth vocabulary gets pinned from a real login attempt.
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
