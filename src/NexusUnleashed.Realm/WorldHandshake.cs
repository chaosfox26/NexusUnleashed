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
    /// Resolves the WORLD-phase cipher keyInteger for the account entering the
    /// world, from the token carried in the 0x058F client hello. The default
    /// returns a fixed dev key so the engine (and the loopback self-test) can
    /// exercise the two-phase re-key MECHANISM without STS wired.
    /// </summary>
    /// <remarks>
    /// The real per-session derivation (SRP session key -> world keyInteger) is
    /// QUARANTINED pending a clean, non-NF source (provenance/QUARANTINE-NF.md):
    /// its formula had been read from the NF-derived tree. The channel, framing,
    /// and message models are all clean; only this resolver's real body waits.
    /// </remarks>
    public static Func<byte[] /*helloBody*/, ulong /*worldKeyInteger*/> WorldKeyResolver { get; set; }
        = _ => DevWorldKey;

    /// <summary>A fixed dev world keyInteger (both ends use it) for the self-test.</summary>
    public const ulong DevWorldKey = 0x4888DCE5CA507060ul;

    public static void Register(GameServer world)
    {
        world.OnConnected = async session =>
        {
            if (session.Crypt == null)
            {
                // Auth channel (23115): the real client accepts a CLEAR 0x0003
                // hello, then speaks the auth-key container protocol (0x0244 in /
                // 0x03DC out). Bootstrap clear, then switch to container mode.
                Log.Info($"realm: client connected {session.RemoteAddress} — clear 0x0003 hello, then container mode");
                await session.SendGameMessageAsync(ServerHello, Hex(HelloBodyHex));
                session.Crypt = new NexusUnleashed.Cryptography.PacketCrypt(NexusUnleashed.Network.WorldPacket.WorldChannelSeed);
            }
            else
            {
                Log.Info($"realm: client connected {session.RemoteAddress} — 0x0003 hello (encrypted 0x03DC)");
                await session.SendGameMessageAsync(ServerHello, Hex(HelloBodyHex));
            }
        };

        // The client's realm-enter (token-bearing). Live 16042 uses inner op
        // 0x0592 (our earlier capture read 0x058F; the live client is authority).
        world.On(0x0592, async (s, body) =>
        {
            Log.Info($"realm: <- 0x0592 realm-enter ({body.Length}B)");
            // The realm reply chain (all client-derived, generated from our DB):
            //   account-info message(s) that clear "Retrieving Account Information"
            //   -> 0x0117 character list -> character select.
            // 0x0117 is the character list (cracked from the client dispatch:
            // 0x117 -> case 0x140021167 -> handler 0x140021540, char stride 0x330).
            // Generator lands next: read the account's characters from characterdb
            // and serialize the client-derived 0x0117 layout. No NF captures.
            await Task.CompletedTask;
        });

        // Log every inner opcode not yet handled — pins the realm vocabulary from
        // the live client, decoded out of its 0x0244 containers.
        world.OnUnhandled = (s, opcode, body) =>
            Log.Info($"realm: <- inner op=0x{opcode:X4} ({body.Length}B) {Preview(body)}");

        world.On(ClientHello, async (s, body) =>
        {
            Log.Info($"world: <- 0x058F client hello ({body.Length}B) {Preview(body)}");
            // The client hello carries the STS game token. Resolve the world
            // keyInteger it maps to, then RE-KEY to the WORLD cipher (two-phase
            // keying): every message after this is enciphered with that key.
            ulong worldKey = WorldKeyResolver(body);
            s.RekeyForWorld(worldKey);
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
