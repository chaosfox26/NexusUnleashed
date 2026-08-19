// NexusUnleashed - clean-room authored. The write side of the bit-packed wire
// format (see PacketReader for provenance). LSB-first within each byte.
using System;
using System.Collections.Generic;

namespace NexusUnleashed.Network;

/// <summary>Writes bit-packed fields, LSB-first, matching the client's reader.</summary>
public sealed class PacketWriter
{
    private readonly List<byte> _data = new();
    private int _bitPos;   // 0..7 within the current (last) byte

    public long BitsWritten => (long)(_data.Count == 0 ? 0 : _data.Count - 1) * 8
                               + (_bitPos == 0 && _data.Count > 0 ? 8 : _bitPos);

    public void WriteBit(bool value)
    {
        if (_bitPos == 0)
            _data.Add(0);
        if (value)
            _data[^1] |= (byte)(1 << _bitPos);
        if (++_bitPos == 8)
            _bitPos = 0;
    }

    public void WriteBits(ulong value, int count)
    {
        if ((uint)count > 64) throw new ArgumentOutOfRangeException(nameof(count));
        for (int i = 0; i < count; i++)
            WriteBit((value & (1UL << i)) != 0);
    }

    public void WriteByte(byte v) => WriteBits(v, 8);
    public void WriteSByte(sbyte v) => WriteBits((byte)v, 8);
    public void WriteUInt16(ushort v) => WriteBits(v, 16);
    public void WriteInt16(short v) => WriteBits((ushort)v, 16);
    public void WriteUInt32(uint v) => WriteBits(v, 32);
    public void WriteInt32(int v) => WriteBits((uint)v, 32);
    public void WriteUInt64(ulong v) => WriteBits(v, 64);
    public void WriteInt64(long v) => WriteBits((ulong)v, 64);
    public void WriteBool(bool v) => WriteBit(v);

    public unsafe void WriteSingle(float v) => WriteUInt32(*(uint*)&v);
    public unsafe void WriteDouble(double v) => WriteUInt64(*(ulong*)&v);

    public void AlignToByte()
    {
        if (_bitPos != 0)
            _bitPos = 0;
    }

    public void WriteWideString(string value)
    {
        foreach (char c in value)
            WriteUInt16(c);
    }

    public void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        AlignToByte();
        foreach (byte b in bytes)
            _data.Add(b);
    }

    /// <summary>Snapshot the written buffer (byte-aligned).</summary>
    public byte[] ToArray()
    {
        AlignToByte();
        return _data.ToArray();
    }
}
