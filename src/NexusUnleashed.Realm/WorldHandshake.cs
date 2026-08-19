// NexusUnleashed - clean-room authored. The world channel handshake, driven by
// the real login capture (spec/protocol/containers.md). On connect the server
// sends the 0x0003 hello inside an encrypted 0x03DC container (exactly the
// captured ServerHello); it then routes the client's follow-up messages toward
// character list / world entry. Bodies flagged UNPINNED are captured templates
// used verbatim until each field is decoded against the client-as-oracle loop.
using System;
using System.Globalization;
using System.Threading.Tasks;
using NexusUnleashed.Network;

namespace NexusUnleashed.Realm;

/// <summary>
/// Wires the world <see cref="GameServer"/>: the on-connect hello and the
/// opcode handlers observed in the capture. Each handler logs what the client
/// sent so the oracle loop (point client at us, read what it rejects) can pin
/// the next message. This is the auth handshake over the encrypted channel.
/// </summary>
public static class WorldHandshake
{
    // Inner opcodes observed on the world channel at login (facts from capture).
    private const ushort ServerHello   = 0x0003;   // S->C first, inside 0x03DC
    private const ushort ClientHello   = 0x058F;   // C->S first real message (token-bearing)
    private const ushort Client07E0    = 0x07E0;
    private const ushort Client038C    = 0x038C;
    private const ushort Client082D    = 0x082D;
    private const ushort ClientState   = 0x0000;

    // UNPINNED: the captured 0x0003 hello body (47 bytes after the opcode), used
    // as a template. Structured/low-churn (aa3e0000 + small counters + a stamp);
    // fields are pinned one at a time. This is our own realm's captured hello.
    private const string HelloBodyHex =
        "aa3e0000010000001500000000000000000000000000000000000b14332f0100000000000000000000000000000000";

    /// <summary>
    /// Resolves the 16-byte SRP session key for the account entering the world,
    /// from the token carried in the 0x058F client hello. The real path looks the
    /// token up against STS-issued sessions; the default returns a fixed dev key
    /// so the engine (and the loopback self-test) can exercise the full two-phase
    /// re-key without STS wired. Replace in deployment with the token store.
    /// </summary>
    public static Func<byte[] /*helloBody*/, byte[] /*sessionKey16*/> SessionKeyResolver { get; set; }
        = _ => DevSessionKey;

    /// <summary>A deterministic 16-byte dev session key (both ends know it).</summary>
    public static readonly byte[] DevSessionKey =
        { 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15 };

    public static void Register(GameServer world)
    {
        world.OnConnected = async session =>
        {
            Log.Info($"world: client connected {session.RemoteAddress} — sending 0x0003 hello (encrypted 0x03DC)");
            await session.SendGameMessageAsync(ServerHello, Hex(HelloBodyHex));
        };

        world.On(ClientHello, async (s, body) =>
        {
            Log.Info($"world: <- 0x058F client hello ({body.Length}B) {Preview(body)}");
            // The client hello carries the STS game token. Resolve the session key
            // it maps to, then RE-KEY to the WORLD cipher (two-phase keying): every
            // message after this is enciphered with GetKeyFromTicket(sessionKey).
            byte[] sessionKey = SessionKeyResolver(body);
            s.RekeyForWorld(sessionKey);
            Log.Info("world: re-keyed to the world cipher; streaming world entry.");
            // Begin the world-entry sequence (spec/protocol/world-entry.md). First
            // the world-init id list; the remaining blobs (0x0988/0x098B/0x0117/
            // 0x0262) are pinned and appended as each payload is generated for the
            // live session.
            await s.SendGameMessageAsync(0x0981, BuildWorldInit());
        });

        world.On(Client07E0, (s, body) => { Log.Info($"world: <- 0x07E0 ({body.Length}B)"); return Task.CompletedTask; });
        world.On(Client038C, (s, body) => { Log.Info($"world: <- 0x038C ({body.Length}B)"); return Task.CompletedTask; });
        world.On(Client082D, (s, body) => { Log.Info($"world: <- 0x082D ({body.Length}B)"); return Task.CompletedTask; });
        world.On(ClientState, (s, body) => { Log.Info($"world: <- 0x0000 State ({body.Length}B)"); return Task.CompletedTask; });
    }

    // World-init body (WITHOUT the opcode; SendGameMessageAsync prepends it):
    // [u32 count][count × u32 id]. The captured shape is a near-sequential set;
    // the id domain is generated for the live session as it is pinned.
    private static byte[] BuildWorldInit()
    {
        var w = new PacketWriter();
        const uint count = 251;
        w.WriteBits(count, 32);
        for (uint i = 1; i <= count; i++) w.WriteBits(i, 32);
        return w.ToArray();
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
        int n = Math.Min(16, b.Length);
        var sb = new System.Text.StringBuilder(n * 2 + 3);
        for (int i = 0; i < n; i++) sb.Append(b[i].ToString("x2"));
        if (b.Length > n) sb.Append('…');
        return sb.ToString();
    }
}
