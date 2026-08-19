// NexusUnleashed - clean-room authored. A per-connection session on modern .NET
// (System.IO.Pipelines async socket). This is our own transport - standard .NET
// infrastructure, no third-party socket code. Frame accumulation follows the
// GamePacketFrame contract; dispatch is handed to the owning server.
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NexusUnleashed.Network;

/// <summary>
/// One client connection. Reads length-framed messages off the socket, hands
/// each complete (opcode, payload) to a dispatcher, and writes frames back.
/// Encryption sits between the socket and the framer once the handshake spec is
/// pinned (the ARC4 keystream from the crypto layer); until then this is the
/// clear-text transport skeleton.
/// </summary>
public sealed class GameSession : IDisposable
{
    private readonly Socket _socket;
    private readonly Func<GameSession, ushort, byte[], Task> _dispatch;
    private readonly CancellationTokenSource _cts = new();

    public Guid Id { get; } = Guid.NewGuid();
    public string RemoteAddress => _socket.RemoteEndPoint?.ToString() ?? "?";

    public GameSession(Socket socket, Func<GameSession, ushort, byte[], Task> dispatch)
    {
        _socket = socket;
        _dispatch = dispatch;
    }

    public Task RunAsync() => ReadLoopAsync(_cts.Token);

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var pipe = new Pipe();
        Task fill = FillAsync(pipe.Writer, ct);
        Task read = ProcessAsync(pipe.Reader, ct);
        await Task.WhenAll(fill, read);
    }

    private async Task FillAsync(PipeWriter writer, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                Memory<byte> mem = writer.GetMemory(4096);
                int read = await _socket.ReceiveAsync(mem, SocketFlags.None, ct);
                if (read == 0) break;
                writer.Advance(read);
                FlushResult fr = await writer.FlushAsync(ct);
                if (fr.IsCompleted) break;
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) { }
        finally { await writer.CompleteAsync(); }
    }

    private async Task ProcessAsync(PipeReader reader, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                ReadResult result = await reader.ReadAsync(ct);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TrySliceFrame(ref buffer, out byte[]? frame))
                {
                    var (opcode, payload) = GamePacketFrame.Decode(frame!);
                    await _dispatch(this, opcode, payload);
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted) break;
            }
        }
        catch (OperationCanceledException) { }
        finally { await reader.CompleteAsync(); }
    }

    private static bool TrySliceFrame(ref ReadOnlySequence<byte> buffer, out byte[]? frame)
    {
        frame = null;
        Span<byte> head = stackalloc byte[GamePacketFrame.SizeFieldBits / 8];
        if (buffer.Length < head.Length)
            return false;
        buffer.Slice(0, head.Length).CopyTo(head);
        if (!GamePacketFrame.TryReadLength(head, out int total))
        {
            // header present but full frame not yet buffered
            if (buffer.Length < total) return false;
        }
        if (buffer.Length < total)
            return false;
        frame = buffer.Slice(0, total).ToArray();
        buffer = buffer.Slice(total);
        return true;
    }

    public async Task SendAsync(IGamePacket packet)
    {
        var w = new PacketWriter();
        packet.Write(w);
        byte[] frame = GamePacketFrame.Encode(packet.Opcode, w.ToArray());
        await _socket.SendAsync(frame, SocketFlags.None);
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _socket.Shutdown(SocketShutdown.Both); } catch { }
        _socket.Dispose();
        _cts.Dispose();
    }
}
