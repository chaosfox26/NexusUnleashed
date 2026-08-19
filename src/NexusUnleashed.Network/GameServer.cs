// NexusUnleashed - clean-room authored. A minimal async TCP acceptor that spins
// a GameSession per connection and routes messages through a registered handler
// table. Modern .NET sockets; our own code end to end.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NexusUnleashed.Cryptography;

namespace NexusUnleashed.Network;

/// <summary>
/// Accepts connections and dispatches decoded messages to handlers keyed by
/// opcode. A handler receives the session and the raw payload bytes; message
/// classes (IGamePacket) decode the payload themselves.
/// </summary>
public sealed class GameServer
{
    private readonly IPEndPoint _endpoint;
    private readonly bool _worldChannel;
    private readonly ConcurrentDictionary<Guid, GameSession> _sessions = new();
    private readonly Dictionary<ushort, Func<GameSession, byte[], Task>> _handlers = new();
    private Socket? _listener;

    /// <summary>
    /// Invoked once per new connection after the session (and its cipher, on the
    /// world channel) is set up. The place to send the server hello.
    /// </summary>
    public Func<GameSession, Task>? OnConnected { get; set; }

    /// <param name="worldChannel">
    /// true = this server speaks the encrypted packed container (world server);
    /// each session gets a PacketCrypt seeded with the static world channel seed.
    /// false = clear direct frames (auth server).
    /// </param>
    public GameServer(string address, int port, bool worldChannel = false)
    {
        _endpoint = new IPEndPoint(IPAddress.Parse(address), port);
        _worldChannel = worldChannel;
    }

    public int SessionCount => _sessions.Count;

    /// <summary>Register a handler for an opcode (a protocol fact).</summary>
    public void On(ushort opcode, Func<GameSession, byte[], Task> handler)
        => _handlers[opcode] = handler;

    /// <summary>
    /// Called for any opcode with no registered handler — the capture hook for
    /// pinning a channel's vocabulary. Receives (session, opcode, payload).
    /// </summary>
    public Action<GameSession, ushort, byte[]>? OnUnhandled { get; set; }

    public async Task ListenAsync(CancellationToken ct = default)
    {
        _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listener.Bind(_endpoint);
        _listener.Listen(128);
        while (!ct.IsCancellationRequested)
        {
            Socket client = await _listener.AcceptAsync(ct);
            _ = HandleClientAsync(client, ct);
        }
    }

    private async Task HandleClientAsync(Socket client, CancellationToken ct)
    {
        var session = new GameSession(client, Dispatch);
        if (_worldChannel)
            session.Crypt = new PacketCrypt(WorldPacket.WorldChannelSeed);
        _sessions[session.Id] = session;
        try
        {
            if (OnConnected != null)
                await OnConnected(session);
            await session.RunAsync();
        }
        finally
        {
            _sessions.TryRemove(session.Id, out _);
            session.Dispose();
        }
    }

    private async Task Dispatch(GameSession session, ushort opcode, byte[] payload)
    {
        if (_handlers.TryGetValue(opcode, out var handler))
            await handler(session, payload);
        else
            OnUnhandled?.Invoke(session, opcode, payload);
        // Unknown opcodes are recorded, never fatal — the client must never be
        // able to crash the server with an unrecognized message.
    }
}
