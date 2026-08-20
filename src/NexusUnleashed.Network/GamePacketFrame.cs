using System;

namespace NexusUnleashed.Network;

public static class GamePacketFrame
{
    public const int SizeFieldBits = 32;    public const int OpcodeFieldBits = 16;
    public static byte[] Encode(ushort opcode, byte[] payload)
    {
        var w = new PacketWriter();
        uint size = (uint)((SizeFieldBits / 8) + (OpcodeFieldBits / 8) + payload.Length);
        w.WriteBits(size, SizeFieldBits);
        w.WriteBits(opcode, OpcodeFieldBits);
        w.WriteBytes(payload);
        return w.ToArray();
    }

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

    public static (ushort Opcode, byte[] Payload) Decode(byte[] frame)
    {
        var r = new PacketReader(frame);
        r.ReadBits(SizeFieldBits);        ushort opcode = (ushort)r.ReadBits(OpcodeFieldBits);
        byte[] payload = r.ReadBytes(r.BytesRemaining);
        return (opcode, payload);
    }
}
