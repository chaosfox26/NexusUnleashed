using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NexusUnleashed.GameData;

public static class TextTable
{
    private const uint Signature = 0x4C544558;
    public static IReadOnlyDictionary<uint, string> Read(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        uint sig = BitConverter.ToUInt32(data, 0);
        if (sig != Signature)
            throw new InvalidDataException($"{Path.GetFileName(path)}: bad TextTable signature 0x{sig:X8}");

        int hsize = 16 + 10 * 8;
        ulong Q(int idx) => BitConverter.ToUInt64(data, 16 + idx * 8);
        ulong recCount = Q(6);
        ulong recOff   = Q(7);
        ulong stLen    = Q(8);
        ulong stOff    = Q(9);

        int stStart = hsize + (int)stOff;
        int stBytes = (int)stLen * 2;
        int stEnd = stStart + stBytes;

        string StringAt(int off)        {
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
            outMap[rid] = StringAt((int)off * 2);        }
        return outMap;
    }
}
