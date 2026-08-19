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
using NexusUnleashed.Cryptography;

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

    /// <summary>
    /// When set, this session speaks the world channel's encrypted packed
    /// container: inbound 0x0244 frames are unwrapped+decrypted to their inner
    /// game message, and <see cref="SendGameMessageAsync"/> wraps+encrypts. When
    /// null, the session is a clear direct-frame channel (the auth server).
    /// </summary>
    public PacketCrypt? Crypt { get; set; }

    /// <summary>Arbitrary per-session state bag for handshake/handlers.</summary>
    public System.Collections.Generic.Dictionary<string, object> State { get; } = new();

    /// <summary>
    /// Switch the channel to the WORLD-phase cipher after login, given the world
    /// keyInteger. Both ends key the same PacketCrypt so all world messages
    /// encrypt/decrypt with it (two-phase keying, proven against the captured
    /// world stream). HOW the world keyInteger is derived from the session key is
    /// the caller's concern — that derivation is quarantined pending a clean,
    /// non-NF source (see PacketCrypt / provenance/QUARANTINE-NF.md).
    /// </summary>
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

                    // World channel: a client container carries one encrypted
                    // inner game message; unwrap+decrypt and dispatch the inner.
                    // A malformed container is contained to this message, never
                    // fatal (the concurrent-multiplayer robustness law).
                    if (Crypt != null && opcode == WorldPacket.ClientContainer)
                    {
                        try
                        {
                            var (innerOp, innerBody) = WorldPacket.DecodeContainer(payload, Crypt);
                            await _dispatch(this, innerOp, innerBody);
                        }
                        catch (ArgumentException) { /* drop the bad container */ }
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
        // `total` is the whole self-inclusive frame length; wait until buffered.
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

    /// <summary>
    /// Send a game message on the world channel. When <see cref="Crypt"/> is set
    /// the message is wrapped in a 0x03DC container and encrypted (exactly the
    /// captured ServerHello path); otherwise it is a clear direct frame.
    /// </summary>
    public async Task SendGameMessageAsync(ushort opcode, byte[] body)
    {
        byte[] frame = Crypt != null
            ? WorldPacket.EncodeServer(opcode, body, Crypt)
            : GamePacketFrame.Encode(opcode, body);
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
