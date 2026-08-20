using System;
using System.Text;

namespace NexusUnleashed.Network;

public sealed class PacketReader
{
    private readonly byte[] _data;
    private int _bytePos;
    private int _bitPos;
    public PacketReader(byte[] data, int offset = 0)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _bytePos = offset;
    }

    public long BitsRead => (long)_bytePos * 8 + _bitPos;

    public int BytesRemaining => _data.Length - _bytePos - (_bitPos > 0 ? 1 : 0);

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

    public void AlignToByte()
    {
        if (_bitPos != 0)
        {
            _bitPos = 0;
            _bytePos++;
        }
    }

    public string ReadWideString(int length)
    {
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append((char)ReadUInt16());
        return sb.ToString();
    }

    public byte[] ReadBytes(int count)
    {
        AlignToByte();
        var result = new byte[count];
        Array.Copy(_data, _bytePos, result, 0, count);
        _bytePos += count;
        return result;
    }
}
