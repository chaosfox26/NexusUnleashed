// NexusUnleashed - clean-room authored. Framing: on-wire, a WildStar message is
// a size-prefixed unit carrying an opcode then a bit-packed payload. The header
// layout is PINNED against the behavioral oracle (spec/protocol/frame.md,
// capture of 2026-08-19): u32 LE size counting the ENTIRE frame including the
// size field itself, then a u16 LE opcode, then the payload.
using System;

namespace NexusUnleashed.Network;

/// <summary>
/// Reads and writes the message envelope around a bit-packed payload.
/// PINNED (spec/protocol/frame.md): size u32 LE includes itself; opcode u16 LE.
/// </summary>
public static class GamePacketFrame
{
    public const int SizeFieldBits = 32;     // PINNED: oracle capture 2026-08-19
    public const int OpcodeFieldBits = 16;   // PINNED: oracle capture 2026-08-19

    /// <summary>
    /// Wrap an opcode + payload into a complete on-wire frame.
    /// </summary>
    public static byte[] Encode(ushort opcode, byte[] payload)
    {
        var w = new PacketWriter();
        // PINNED: size counts the whole frame - size field + opcode + payload.
        uint size = (uint)((SizeFieldBits / 8) + (OpcodeFieldBits / 8) + payload.Length);
        w.WriteBits(size, SizeFieldBits);
        w.WriteBits(opcode, OpcodeFieldBits);
        w.WriteBytes(payload);
        return w.ToArray();
    }

    /// <summary>
    /// Peek the declared frame length so a stream reader knows how many bytes
    /// constitute one complete message. The size field is self-inclusive, so
    /// the declared value IS the total frame length.
    /// </summary>
    public static bool TryReadLength(ReadOnlySpan<byte> buffer, out int totalBytes)
    {
        totalBytes = 0;
        int headerBytes = SizeFieldBits / 8;
        if (buffer.Length < headerBytes)
            return false;
        var r = new PacketReader(buffer.ToArray());
        totalBytes = (int)r.ReadBits(SizeFieldBits);
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
