// NexusUnleashed - clean-room authored. Async STS listener: one StsSession per
// connection, requests routed by "/Service/Message" URI (the message set is
// measured from the client - spec/protocol/sts.md).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NexusUnleashed.Sts;

public sealed class StsServer
{
    private readonly IPEndPoint _endpoint;
    private readonly Dictionary<string, Func<StsSession, StsRequest, Task>> _routes =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, StsSession> _sessions = new();

    public StsServer(string address, int port)
        => _endpoint = new IPEndPoint(IPAddress.Parse(address), port);

    public int SessionCount => _sessions.Count;

    /// <summary>
    /// Called for EVERY inbound request before routing — the clean capture hook.
    /// When a real client logs in, this records exactly what it sends (the STS
    /// port is clear-text), which pins the login XML schema without reading any
    /// NF source. Never throws into dispatch.
    /// </summary>
    public Action<StsRequest>? RequestObserver { get; set; }

    /// <summary>Route an STS URI ("/Auth/LoginStart") to a handler.</summary>
    public void On(string uri, Func<StsSession, StsRequest, Task> handler)
        => _routes[uri] = handler;

    public async Task ListenAsync(CancellationToken ct = default)
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(_endpoint);
        listener.Listen(64);
        while (!ct.IsCancellationRequested)
        {
            Socket client = await listener.AcceptAsync(ct);
            var session = new StsSession(client, DispatchAsync);
            _sessions[session.Id] = session;
            _ = RunSessionAsync(session);
        }
    }

    private async Task RunSessionAsync(StsSession session)
    {
        try { await session.RunAsync(); }
        finally { _sessions.TryRemove(session.Id, out _); session.Dispose(); }
    }

    private async Task DispatchAsync(StsSession session, StsRequest request)
    {
        if (RequestObserver != null)
        {
            try { RequestObserver(request); } catch { /* capture must never break login */ }
        }

        if (_routes.TryGetValue(request.Uri, out var handler))
        {
            try
            {
                await handler(session, request);
            }
            catch (Exception ex)
            {
                // A handler fault must never leave the client hanging (it would
                // just ping forever). Log it and reply ERROR so the failure is
                // visible on both ends.
                Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff} [ERROR] STS handler {request.Uri} threw: {ex.GetType().Name}: {ex.Message}");
                try { await session.SendAsync(StsReply.Error(request.Sequence, 500)); } catch { }
            }
        }
        else
        {
            // Unknown message: reply ERROR, never drop the connection - the
            // client must not be able to wedge the login server.
            await session.SendAsync(StsReply.Error(request.Sequence, 400));
        }
    }
}

/// <summary>One STS connection; carries the per-login auth state.</summary>
public sealed class StsSession : IDisposable
{
    private readonly Socket _socket;
    private readonly Func<StsSession, StsRequest, Task> _dispatch;
    private readonly StsParser _parser = new();

    public Guid Id { get; } = Guid.NewGuid();
    public string RemoteAddress => _socket.RemoteEndPoint?.ToString() ?? "?";

    /// <summary>Per-session state bag (the auth flow keeps its SRP state here).</summary>
    public Dictionary<string, object> State { get; } = new();

    public StsSession(Socket socket, Func<StsSession, StsRequest, Task> dispatch)
    {
        _socket = socket;
        _dispatch = dispatch;
    }

    public async Task RunAsync()
    {
        var buf = new byte[8192];
        while (true)
        {
            int read;
            try { read = await _socket.ReceiveAsync(buf, SocketFlags.None); }
            catch (SocketException) { break; }
            if (read == 0) break;

            _parser.Feed(buf.AsSpan(0, read));
            StsRequest? req;
            while ((req = _parser.TryReadRequest()) != null)
                await _dispatch(this, req);
        }
    }

    public async Task SendAsync(byte[] frame)
    {
        // Reply logging (capture stage): dump exactly what we send so a rejected
        // reply can be compared against what the client expects.
        try
        {
            string text = System.Text.Encoding.UTF8.GetString(frame);
            Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff} [STS-REPLY] {text.Replace("\r", "\\r").Replace("\n", "\\n")}");
        }
        catch { }
        await _socket.SendAsync(frame, SocketFlags.None);
    }

    public void Dispose()
    {
        try { _socket.Shutdown(SocketShutdown.Both); } catch { }
        _socket.Dispose();
    }
}
