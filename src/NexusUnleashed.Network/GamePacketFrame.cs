// NexusUnleashed - clean-room authored. Framing: on-wire, a WildStar message is
// a size-prefixed unit carrying an opcode then a bit-packed payload. The exact
// header layout (size width, opcode width, endianness) is a client protocol
// FACT and is pinned by a spec entry validated against the behavioral oracle,
// never guessed and never copied. The values below are placeholders marked
// UNPINNED until confirmed by capture; the SHAPE (size, opcode, payload) is
// authored here.
using System;

namespace NexusUnleashed.Network;

/// <summary>
/// Reads and writes the message envelope around a bit-packed payload.
///
/// SPEC STATUS: header widths are pinned in spec/protocol/frame.md against the
/// oracle before this leaves placeholder state. The class shape is final; the
/// three constants are the only thing awaiting a capture.
/// </summary>
public static class GamePacketFrame
{
    // UNPINNED (spec/protocol/frame.md): to be fixed by an oracle capture.
    public const int SizeFieldBits = 32;
    public const int OpcodeFieldBits = 16;

    /// <summary>
    /// Wrap an opcode + payload into a complete on-wire frame.
    /// </summary>
    public static byte[] Encode(ushort opcode, byte[] payload)
    {
        var w = new PacketWriter();
        // size = opcode field + payload bytes (spec-pinned interpretation)
        uint size = (uint)((OpcodeFieldBits / 8) + payload.Length);
        w.WriteBits(size, SizeFieldBits);
        w.WriteBits(opcode, OpcodeFieldBits);
        w.WriteBytes(payload);
        return w.ToArray();
    }

    /// <summary>
    /// Peek the declared frame length (size field + its own width) so a stream
    /// reader knows how many bytes constitute one complete message.
    /// </summary>
    public static bool TryReadLength(ReadOnlySpan<byte> buffer, out int totalBytes)
    {
        totalBytes = 0;
        int headerBytes = SizeFieldBits / 8;
        if (buffer.Length < headerBytes)
            return false;
        var r = new PacketReader(buffer.ToArray());
        uint size = (uint)r.ReadBits(SizeFieldBits);
        totalBytes = headerBytes + (int)size;
        return buffer.Length >= totalBytes;
    }

    /// <summary>Split a complete frame into (opcode, payload).</summary>
    public static (ushort Opcode, byte[] Payload) Decode(byte[] frame)
    {
        var r = new PacketReader(frame);
        r.ReadBits(SizeFieldBits);                 // size (already used to slice)
        ushort opcode = (ushort)r.ReadBits(OpcodeFieldBits);
        byte[] payload = r.ReadBytes(r.BytesRemaining);
        return (opcode, payload);
    }
}
