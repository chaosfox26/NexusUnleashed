// NexusUnleashed - clean-room authored. Reads Carbine's localization .bin
// (en-US.bin etc.): id -> localized string. Format ported from our own
// tbl_reader.read_text_table (equivalence-gated). Signature 'XETL' 0x4C544558,
// header = u32 sig/ver/lang/unk1 then 10 u64 (tag, short, long, records, string
// table). Records are (id u32, offset u32); the offset is in UTF-16 code units.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NexusUnleashed.GameData;

public static class TextTable
{
    private const uint Signature = 0x4C544558;   // 'XETL'

    public static IReadOnlyDictionary<uint, string> Read(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        uint sig = BitConverter.ToUInt32(data, 0);
        if (sig != Signature)
            throw new InvalidDataException($"{Path.GetFileName(path)}: bad TextTable signature 0x{sig:X8}");

        // header: u32 sig, ver, lang, unk1 (16 bytes) then 10 u64, which are
        // tag_len, tag_off, short_len, short_off, long_len, long_off,
        // rec_count, rec_off, st_len, st_off (indices 0..9).
        int hsize = 16 + 10 * 8;
        ulong Q(int idx) => BitConverter.ToUInt64(data, 16 + idx * 8);
        ulong recCount = Q(6);
        ulong recOff   = Q(7);
        ulong stLen    = Q(8);
        ulong stOff    = Q(9);

        int stStart = hsize + (int)stOff;
        int stBytes = (int)stLen * 2;
        int stEnd = stStart + stBytes;

        string StringAt(int off)   // off in bytes within the string table
        {
            int p = stStart + off;
            if (off < 0 || p >= stEnd) return "";
            int end = p;
            while (end + 1 < stEnd && !(data[end] == 0 && data[end + 1] == 0))
                end += 2;
            return Encoding.Unicode.GetString(data, p, end - p);
        }

        var outMap = new Dictionary<uint, string>((int)recCount);
        int pos = hsize + (int)recOff;
        for (ulong i = 0; i < recCount; i++)
        {
            uint rid = BitConverter.ToUInt32(data, pos);
            uint off = BitConverter.ToUInt32(data, pos + 4);
            pos += 8;
            outMap[rid] = StringAt((int)off * 2);   // offset is in UTF-16 units
        }
        return outMap;
    }
}
