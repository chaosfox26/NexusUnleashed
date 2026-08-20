using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NexusUnleashed.Cryptography;

namespace NexusUnleashed.Network;

public sealed class GameServer
{
    private readonly IPEndPoint _endpoint;
    private readonly bool _worldChannel;
    private readonly ConcurrentDictionary<Guid, GameSession> _sessions = new();
    private readonly Dictionary<ushort, Func<GameSession, byte[], Task>> _handlers = new();
    private Socket? _listener;

    public Func<GameSession, Task>? OnConnected { get; set; }

    public GameServer(string address, int port, bool worldChannel = false)
    {
        _endpoint = new IPEndPoint(IPAddress.Parse(address), port);
        _worldChannel = worldChannel;
    }

    public int SessionCount => _sessions.Count;

    public void On(ushort opcode, Func<GameSession, byte[], Task> handler)
        => _handlers[opcode] = handler;

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
    }
}
