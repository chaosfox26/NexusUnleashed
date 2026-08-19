// NexusUnleashed - clean-room authored.
// Provenance: the WildStar wire format is BIT-PACKED, little-endian, LSB-first
// within each byte - a fact of Carbine's client protocol (documented in our own
// datamine). This reader is our own implementation of that fact; the bit-stream
// algorithm is standard and belongs to no one.
using System;
using System.Text;

namespace NexusUnleashed.Network;

/// <summary>
/// Reads bit-packed fields from a buffer, LSB-first within each byte, in the
/// order the WildStar client emits them. All game messages are a sequence of
/// such reads.
/// </summary>
public sealed class PacketReader
{
    private readonly byte[] _data;
    private int _bytePos;
    private int _bitPos;   // 0..7, LSB-first

    public PacketReader(byte[] data, int offset = 0)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _bytePos = offset;
    }

    /// <summary>Total bits consumed so far.</summary>
    public long BitsRead => (long)_bytePos * 8 + _bitPos;

    /// <summary>Bytes remaining (whole bytes only).</summary>
    public int BytesRemaining => _data.Length - _bytePos - (_bitPos > 0 ? 1 : 0);

    /// <summary>Read a single bit as a bool.</summary>
    public bool ReadBit()
    {
        bool value = (_data[_bytePos] & (1 << _bitPos)) != 0;
        if (++_bitPos == 8)
        {
            _bitPos = 0;
            _bytePos++;
        }
        return value;
    }

    /// <summary>Read up to 64 bits as an unsigned value, LSB-first.</summary>
    public ulong ReadBits(int count)
    {
        if ((uint)count > 64) throw new ArgumentOutOfRangeException(nameof(count));
        ulong value = 0;
        for (int i = 0; i < count; i++)
            if (ReadBit())
                value |= 1UL << i;
        return value;
    }

    public byte ReadByte() => (byte)ReadBits(8);
    public sbyte ReadSByte() => (sbyte)ReadBits(8);
    public ushort ReadUInt16() => (ushort)ReadBits(16);
    public short ReadInt16() => (short)ReadBits(16);
    public uint ReadUInt32() => (uint)ReadBits(32);
    public int ReadInt32() => (int)ReadBits(32);
    public ulong ReadUInt64() => ReadBits(64);
    public long ReadInt64() => (long)ReadBits(64);
    public bool ReadBool() => ReadBit();

    public unsafe float ReadSingle()
    {
        uint bits = ReadUInt32();
        return *(float*)&bits;
    }

    public unsafe double ReadDouble()
    {
        ulong bits = ReadUInt64();
        return *(double*)&bits;
    }

    /// <summary>Align the cursor to the next whole byte boundary.</summary>
    public void AlignToByte()
    {
        if (_bitPos != 0)
        {
            _bitPos = 0;
            _bytePos++;
        }
    }

    /// <summary>
    /// Read a UTF-16LE wide string of <paramref name="length"/> chars. WildStar
    /// strings are length-prefixed elsewhere; the prefix width is the caller's
    /// (message-level) concern.
    /// </summary>
    public string ReadWideString(int length)
    {
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append((char)ReadUInt16());
        return sb.ToString();
    }

    /// <summary>Read <paramref name="count"/> raw bytes (byte-aligns first).</summary>
    public byte[] ReadBytes(int count)
    {
        AlignToByte();
        var result = new byte[count];
        Array.Copy(_data, _bytePos, result, 0, count);
        _bytePos += count;
        return result;
    }
}
