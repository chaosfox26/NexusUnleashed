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

    public Action<StsRequest>? RequestObserver { get; set; }

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
            try { RequestObserver(request); } catch { }
        }

        if (_routes.TryGetValue(request.Uri, out var handler))
        {
            try
            {
                await handler(session, request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff} [ERROR] STS handler {request.Uri} threw: {ex.GetType().Name}: {ex.Message}");
                try { await session.SendAsync(StsReply.Error(request.Sequence, 500)); } catch { }
            }
        }
        else
        {
            await session.SendAsync(StsReply.Error(request.Sequence, 400));
        }
    }
}

public sealed class StsSession : IDisposable
{
    private readonly Socket _socket;
    private readonly Func<StsSession, StsRequest, Task> _dispatch;
    private readonly StsParser _parser = new();

    public Guid Id { get; } = Guid.NewGuid();
    public string RemoteAddress => _socket.RemoteEndPoint?.ToString() ?? "?";

    public Dictionary<string, object> State { get; } = new();

    private Arc4? _rx, _tx;
    public StsSession(Socket socket, Func<StsSession, StsRequest, Task> dispatch)
    {
        _socket = socket;
        _dispatch = dispatch;
    }

    public void EnableEncryption(byte[] sessionKey)
    {
        _rx = new Arc4(sessionKey);
        _tx = new Arc4(sessionKey);
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

            if (_rx != null) _rx.Process(buf, read);
            _parser.Feed(buf.AsSpan(0, read));
            StsRequest? req;
            while ((req = _parser.TryReadRequest()) != null)
                await _dispatch(this, req);
        }
    }

    public async Task SendAsync(byte[] frame)
    {
        try { Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff} [STS-REPLY{(_tx == null ? "" : "-ENC")}] {System.Text.Encoding.UTF8.GetString(frame).Replace("\r", "\\r").Replace("\n", "\\n")}"); } catch { }
        if (_tx != null)
        {
            frame = (byte[])frame.Clone();
            _tx.Process(frame, frame.Length);        }
        await _socket.SendAsync(frame, SocketFlags.None);
    }

    public void Dispose()
    {
        try { _socket.Shutdown(SocketShutdown.Both); } catch { }
        _socket.Dispose();
    }
}

internal sealed class Arc4
{
    private readonly byte[] _s = new byte[256];
    private int _i, _j;
    public Arc4(byte[] key)
    {
        for (int i = 0; i < 256; i++) _s[i] = (byte)i;
        int j = 0;
        for (int i = 0; i < 256; i++)
        {
            j = (j + _s[i] + key[i % key.Length]) & 0xff;
            (_s[i], _s[j]) = (_s[j], _s[i]);
        }
    }
    public void Process(byte[] data, int len)
    {
        for (int n = 0; n < len; n++)
        {
            _i = (_i + 1) & 0xff;
            _j = (_j + _s[_i]) & 0xff;
            (_s[_i], _s[_j]) = (_s[_j], _s[_i]);
            data[n] ^= _s[(_s[_i] + _s[_j]) & 0xff];
        }
    }
}
