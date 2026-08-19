// NexusUnleashed - clean-room authored. A thin dispatch layer over GameServer:
// registers opcode-keyed handlers and logs the first sighting of any opcode that
// is handled but not yet pinned in the Opcodes registry (never fatal). As world
// messages are pinned from captures, their handlers register here.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NexusUnleashed.Network;

public sealed class WorldMessageRouter
{
    private readonly HashSet<ushort> _seenUnpinned = new();
    private readonly Action<string>? _log;

    public WorldMessageRouter(Action<string>? log = null) => _log = log;

    /// <summary>Register a handler for one opcode on the server.</summary>
    public void On(GameServer server, ushort opcode, Func<GameSession, byte[], Task> handler)
    {
        server.On(opcode, (session, payload) =>
        {
            if (!Opcodes.TryGet(opcode, out _) && _seenUnpinned.Add(opcode))
                _log?.Invoke($"handling opcode {Opcodes.NameOf(opcode)} (not yet pinned in the registry)");
            return handler(session, payload);
        });
    }
}
