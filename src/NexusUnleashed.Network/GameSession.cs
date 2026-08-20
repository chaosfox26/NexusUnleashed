using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NexusUnleashed.Cryptography;

namespace NexusUnleashed.Network;

public sealed class GameSession : IDisposable
{
    private readonly Socket _socket;
    private readonly Func<GameSession, ushort, byte[], Task> _dispatch;
    private readonly CancellationTokenSource _cts = new();

    public Guid Id { get; } = Guid.NewGuid();
    public string RemoteAddress => _socket.RemoteEndPoint?.ToString() ?? "?";

    public PacketCrypt? Crypt { get; set; }

    public System.Collections.Generic.Dictionary<string, object> State { get; } = new();

    public void RekeyForWorld(ulong worldKeyInteger)
        => Crypt = new PacketCrypt(worldKeyInteger);

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

                    if (Crypt != null && opcode == WorldPacket.ClientContainer)
                    {
                        try
                        {
                            var (innerOp, innerBody) = WorldPacket.DecodeContainer(payload, Crypt);
                            await _dispatch(this, innerOp, innerBody);
                        }
                        catch (ArgumentException) { }
                    }
                    else
                    {
                        await _dispatch(this, opcode, payload);
                    }
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
        GamePacketFrame.TryReadLength(head, out int total);
        if (total < head.Length || buffer.Length < total)
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

    public async Task SendGameMessageAsync(ushort opcode, byte[] body)
    {
        byte[] frame = Crypt != null
            ? WorldPacket.EncodeServer(opcode, body, Crypt)
            : GamePacketFrame.Encode(opcode, body);
        await _socket.SendAsync(frame, SocketFlags.None);
    }

    public async Task SendClearGameMessageAsync(ushort opcode, byte[] body)
    {
        byte[] frame = GamePacketFrame.Encode(opcode, body);
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
