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

namespace NexusUnleashed.Network;

/// <summary>
/// Accepts connections and dispatches decoded messages to handlers keyed by
/// opcode. A handler receives the session and the raw payload bytes; message
/// classes (IGamePacket) decode the payload themselves.
/// </summary>
public sealed class GameServer
{
    private readonly IPEndPoint _endpoint;
    private readonly ConcurrentDictionary<Guid, GameSession> _sessions = new();
    private readonly Dictionary<ushort, Func<GameSession, byte[], Task>> _handlers = new();
    private Socket? _listener;

    public GameServer(string address, int port)
        => _endpoint = new IPEndPoint(IPAddress.Parse(address), port);

    public int SessionCount => _sessions.Count;

    /// <summary>Register a handler for an opcode (a protocol fact).</summary>
    public void On(ushort opcode, Func<GameSession, byte[], Task> handler)
        => _handlers[opcode] = handler;

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
        _sessions[session.Id] = session;
        try
        {
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
        // Unknown opcodes are recorded, never fatal (the engine's own gaps log
        // will note them once wired) — the client must never be able to crash
        // the server with an unrecognized message.
    }
}
