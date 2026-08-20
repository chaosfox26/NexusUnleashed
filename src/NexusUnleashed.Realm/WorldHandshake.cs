using System;
using System.Globalization;
using System.Threading.Tasks;
using NexusUnleashed.Network;

namespace NexusUnleashed.Realm;

public static class WorldHandshake
{
    private const ushort ServerHello   = 0x0003;    private const ushort ClientHello   = 0x058F;    private const ushort Client07E0    = 0x07E0;
    private const ushort Client038C    = 0x038C;
    private const ushort Client082D    = 0x082D;
    private const ushort ClientState   = 0x0000;

    private const string HelloBodyHex =
        "aa3e0000010000001500000000000000000000000000000000000b14332f0100000000000000000000000000000000";

    public static Func<byte[], ulong> WorldKeyResolver { get; set; }
        = _ => DevWorldKey;

    public const ulong DevWorldKey = 0x4888DCE5CA507060ul;

    public static Func<long, Task<byte[]?>>? CharacterListBodyProvider { get; set; }

    public static void Register(GameServer world)
    {
        world.OnConnected = async session =>
        {
            if (session.Crypt == null)
            {
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

        world.On(0x0592, async (s, body) =>
        {
            Log.Info($"realm: <- 0x0592 realm-enter ({body.Length}B)");
            long acc = NexusUnleashed.Sts.AuthSession.LastAccountId;
            var provider = CharacterListBodyProvider;
            if (provider == null)
            {
                Log.Info("realm: no character-list provider wired");
                return;
            }
            try
            {
                byte[]? charListBody = await provider(acc);
                if (charListBody == null)
                {
                    Log.Info($"realm: character-list provider returned null for account {acc}");
                    return;
                }
                await s.SendClearGameMessageAsync(NexusUnleashed.Network.CharacterListMessage.Opcode, charListBody);
                Log.Info($"realm: -> 0x0117 character list (clear frame) for account {acc} ({charListBody.Length}B)");
            }
            catch (Exception ex)
            {
                Log.Info($"realm: character-list send failed: {ex.Message}");
            }
        });

        world.OnUnhandled = (s, opcode, body) =>
            Log.Info($"realm: <- inner op=0x{opcode:X4} ({body.Length}B) {Preview(body)}");

        world.On(ClientHello, async (s, body) =>
        {
            Log.Info($"world: <- 0x058F client hello ({body.Length}B) {Preview(body)}");
            ulong worldKey = WorldKeyResolver(body);
            s.RekeyForWorld(worldKey);
            Log.Info("world: re-keyed to the world cipher; streaming world entry.");
            await s.SendGameMessageAsync(0x0981, BuildWorldInit());
        });

        world.On(Client07E0, (s, body) => { Log.Info($"world: <- 0x07E0 ({body.Length}B)"); return Task.CompletedTask; });
        world.On(Client038C, (s, body) => { Log.Info($"world: <- 0x038C ({body.Length}B)"); return Task.CompletedTask; });
        world.On(Client082D, (s, body) => { Log.Info($"world: <- 0x082D ({body.Length}B)"); return Task.CompletedTask; });
        world.On(ClientState, (s, body) => { Log.Info($"world: <- 0x0000 State ({body.Length}B)"); return Task.CompletedTask; });
    }

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
