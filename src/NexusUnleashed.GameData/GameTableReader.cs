// NexusUnleashed - clean-room authored. Reads Carbine's .tbl (GameTable) binary
// format. Provenance: the format is documented entirely in OUR OWN datamine and
// implemented in our Python tbl_reader.py (equivalence-gated to 10.27M values
// against the engine). The binary layout is a fact about Carbine's files; this
// C# reader is authored from our own spec, not from any server's source.
//
// Layout (our datamine): signature 'LBTD' 0x4454424C, then a 96-byte header
// (Version + 11 u64), field defs (24 bytes each: nameLen u64, nameOff u64,
// type u16, +u16 +u32), a 16-aligned UTF-16LE column-name blob, fixed-size
// records, then a UTF-16LE string table.
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
    /// <summary>Row values, boxed by column. uint / float / ulong / string / bool.</summary>
    public IReadOnlyList<object[]> Rows = Array.Empty<object[]>();
}

public static class GameTableReader
{
    private const uint Signature = 0x4454424C;   // 'LBTD'
    private const int HeaderSize = 96;
    private const int FieldSize = 24;

    public static GameTable Read(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        var s = new SpanCursor(data);

        uint sig = s.U32(0);
        if (sig != Signature)
            throw new InvalidDataException($"{Path.GetFileName(path)}: bad signature 0x{sig:X8}");

        // header: u32 sig, u32 version, then 11 u64 (offsets from our datamine
        // spec / tbl_reader.py): nameLen@8, unk1@16, recordSize@24,
        // fieldCount@32, fieldOffset@40, recordCount@48, totalRecordSize@56,
        // recordOffset@64, maxId@72, lookupOffset@80, unk2@88.
        ulong recordSize = s.U64(24);
        ulong fieldCount = s.U64(32);
        ulong fieldOffset = s.U64(40);
        ulong recordCount = s.U64(48);
        ulong totalRecordSize = s.U64(56);
        ulong recordOffset = s.U64(64);

        // field definitions
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

        // column names: 16-aligned blob between field defs and records
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

        // string table sits after the fixed-size records
        long recordsBytes = (long)recordSize * (long)recordCount;
        int stOff = HeaderSize + (int)recordOffset + (int)recordsBytes;
        int stLen = (int)((long)totalRecordSize - recordsBytes);

        string StringAt(int off)
        {
            // UTF-16LE, double-null terminated, char-aligned
            int p = stOff + off;
            int end = p;
            while (end + 1 < stOff + stLen && !(data[end] == 0 && data[end + 1] == 0))
                end += 2;
            return Encoding.Unicode.GetString(data, p, end - p);
        }

        var rows = new List<object[]>((int)recordCount);
        for (ulong j = 0; j < recordCount; j++)
        {
            int rp = HeaderSize + (int)recordOffset + (int)recordSize * (int)j;
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
            }
            rows.Add(row);
        }

        return new GameTable
        {
            Name = Path.GetFileNameWithoutExtension(path),
            Fields = cols,
            Rows = rows,
        };
    }

    /// <summary>Little-endian primitive reads over the file bytes.</summary>
    private readonly struct SpanCursor
    {
        private readonly byte[] _d;
        public SpanCursor(byte[] d) => _d = d;
        public uint U32(int o) => BitConverter.ToUInt32(_d, o);
        public ulong U64(int o) => BitConverter.ToUInt64(_d, o);
        public ushort U16(int o) => BitConverter.ToUInt16(_d, o);
    }
}
