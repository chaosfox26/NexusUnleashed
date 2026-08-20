using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NexusUnleashed.GameData;

public enum FieldType : ushort
{
    UInt = 3,
    Float = 4,
    Bool = 11,
    ULong = 20,
    String = 130,
}

public sealed class GameField
{
    public string Name = "";
    public FieldType Type;
}

public sealed class GameTable
{
    public string Name = "";
    public IReadOnlyList<GameField> Fields = Array.Empty<GameField>();
    public IReadOnlyList<object[]> Rows = Array.Empty<object[]>();
}

public static class GameTableReader
{
    private const uint Signature = 0x4454424C;    private const int HeaderSize = 96;
    private const int FieldSize = 24;

    public static GameTable ReadSchema(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        var s = new SpanCursor(data);
        if (s.U32(0) != Signature)
            throw new InvalidDataException($"{Path.GetFileName(path)}: bad signature");

        ulong fieldCount = s.U64(32);
        ulong fieldOffset = s.U64(40);
        var cols = ReadFields(data, s, (int)fieldOffset, (int)fieldCount);
        return new GameTable { Name = Path.GetFileNameWithoutExtension(path), Fields = cols };
    }

    private static List<GameField> ReadFields(byte[] data, SpanCursor s, int fieldOffset, int fieldCount)
    {
        var defs = new List<(int nameLen, int nameOff, FieldType type)>();
        int pos = HeaderSize + fieldOffset;
        for (int i = 0; i < fieldCount; i++)
        {
            defs.Add(((int)s.U64(pos), (int)s.U64(pos + 8), (FieldType)s.U16(pos + 16)));
            pos += FieldSize;
        }
        int namesStart = (HeaderSize + fieldOffset + FieldSize * fieldCount + 15) & ~15;
        var cols = new List<GameField>(fieldCount);
        for (int i = 0; i < defs.Count; i++)
        {
            var (nameLen, nameOff, ftype) = defs[i];
            string name = nameLen > 1
                ? Encoding.Unicode.GetString(data, namesStart + nameOff, (nameLen - 1) * 2)
                : $"col{i}";
            cols.Add(new GameField { Name = name, Type = ftype });
        }
        return cols;
    }

    public static GameTable Read(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        var s = new SpanCursor(data);

        uint sig = s.U32(0);
        if (sig != Signature)
            throw new InvalidDataException($"{Path.GetFileName(path)}: bad signature 0x{sig:X8}");

        ulong recordSize = s.U64(24);
        ulong fieldCount = s.U64(32);
        ulong fieldOffset = s.U64(40);
        ulong recordCount = s.U64(48);
        ulong totalRecordSize = s.U64(56);
        ulong recordOffset = s.U64(64);

        var fields = new List<(int nameLen, int nameOff, FieldType type)>();
        int pos = HeaderSize + (int)fieldOffset;
        for (ulong i = 0; i < fieldCount; i++)
        {
            int nameLen = (int)s.U64(pos);
            int nameOff = (int)s.U64(pos + 8);
            var ftype = (FieldType)s.U16(pos + 16);
            fields.Add((nameLen, nameOff, ftype));
            pos += FieldSize;
        }

        int namesStart = (HeaderSize + (int)fieldOffset + FieldSize * (int)fieldCount + 15) & ~15;
        var cols = new List<GameField>();
        for (int i = 0; i < fields.Count; i++)
        {
            var (nameLen, nameOff, ftype) = fields[i];
            string name = nameLen > 1
                ? Encoding.Unicode.GetString(data, namesStart + nameOff, (nameLen - 1) * 2)
                : $"col{i}";
            cols.Add(new GameField { Name = name, Type = ftype });
        }

        long recordsBytes = (long)recordSize * (long)recordCount;
        int stOff = HeaderSize + (int)recordOffset + (int)recordsBytes;
        int stLen = (int)((long)totalRecordSize - recordsBytes);

        int stEnd = stOff + stLen;
        string StringAt(int off)
        {
            int p = stOff + off;
            if (off < 0 || p >= stEnd) return "";
            int end = p;
            while (end + 1 < stEnd && !(data[end] == 0 && data[end + 1] == 0))
                end += 2;
            return Encoding.Unicode.GetString(data, p, end - p);
        }

        int[] widths = new int[fields.Count];
        int baseWidth = 0;
        for (int f = 0; f < fields.Count; f++)
        {
            widths[f] = (fields[f].type == FieldType.ULong || fields[f].type == FieldType.String) ? 8 : 4;
            baseWidth += widths[f];
        }

        var candidates = new List<int>();
        if (recordCount > 0)
        {
            int cp = HeaderSize + (int)recordOffset;
            for (int f = 0; f < fields.Count; f++)
            {
                if (fields[f].type == FieldType.String)
                {
                    uint a0 = s.U32(cp);
                    if (a0 == 0 && f < fields.Count - 1 && fields[f + 1].type != FieldType.String)
                        candidates.Add(f);
                }
                cp += widths[f];
            }
        }

        int extra = (int)recordSize - baseWidth;
        if (extra < 0 || extra % 4 != 0)
            throw new InvalidDataException($"{Path.GetFileName(path)}: record arithmetic broken (size {recordSize}, base {baseWidth})");
        while (candidates.Count > 0 && candidates.Count * 4 > extra)
            candidates.RemoveAt(candidates.Count - 1);
        if (candidates.Count * 4 != extra)
            throw new InvalidDataException($"{Path.GetFileName(path)}: cannot close record arithmetic (extra {extra}, pads {candidates.Count})");
        var padAfter = new HashSet<int>(candidates);

        var rows = new List<object[]>((int)recordCount);
        for (ulong j = 0; j < recordCount; j++)
        {
            int start = HeaderSize + (int)recordOffset + (int)recordSize * (int)j;
            int rp = start;
            var row = new object[fields.Count];
            for (int f = 0; f < fields.Count; f++)
            {
                switch (fields[f].type)
                {
                    case FieldType.UInt:
                    case FieldType.Bool:
                        row[f] = s.U32(rp); rp += 4; break;
                    case FieldType.Float:
                        row[f] = BitConverter.ToSingle(data, rp); rp += 4; break;
                    case FieldType.ULong:
                        row[f] = s.U64(rp); rp += 8; break;
                    case FieldType.String:
                        uint a = s.U32(rp), b = s.U32(rp + 4);
                        rp += 8;
                        row[f] = StringAt((int)(Math.Max(a, b) - (ulong)recordsBytes));
                        break;
                    default:
                        row[f] = s.U32(rp); rp += 4; break;
                }
                if (padAfter.Contains(f)) rp += 4;
            }
            if (rp - start != (int)recordSize)
                throw new InvalidDataException($"{Path.GetFileName(path)}: row {j} consumed {rp - start} of {recordSize} bytes");
            rows.Add(row);
        }

        return new GameTable
        {
            Name = Path.GetFileNameWithoutExtension(path),
            Fields = cols,
            Rows = rows,
        };
    }

    private readonly struct SpanCursor
    {
        private readonly byte[] _d;
        public SpanCursor(byte[] d) => _d = d;
        public uint U32(int o) => BitConverter.ToUInt32(_d, o);
        public ulong U64(int o) => BitConverter.ToUInt64(_d, o);
        public ushort U16(int o) => BitConverter.ToUInt16(_d, o);
    }
}
