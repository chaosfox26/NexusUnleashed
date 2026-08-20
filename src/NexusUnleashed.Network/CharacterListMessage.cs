using System.Collections.Generic;

namespace NexusUnleashed.Network;

public sealed class CharacterRecord
{
    public ulong Id;
    public string Name = "";
    public uint Sex;    public uint Race;    public uint Class;    public uint Level;    public uint FactionId;    public float LocationX;
    public float LocationY;
    public float LocationZ;
    public uint WorldId;
}

public static class CharacterListMessage
{
    public const ushort Opcode = 0x0117;

    public static byte[] Build(IReadOnlyList<CharacterRecord> characters)
    {
        var w = new PacketWriter();

        w.WriteUInt64(0);        w.WriteUInt32((uint)characters.Count);
        foreach (var c in characters)
            WriteCharacter(w, c);

        w.WriteUInt32(0);        w.WriteUInt32(0);        w.WriteBits(0, 14);        w.WriteBits(0, 14); w.WriteUInt64(0);        w.WriteUInt32(0);        w.WriteUInt32(0);        w.WriteUInt32(0);        w.WriteUInt32(0);        w.WriteBits(0, 14);        w.WriteBit(false);
        return w.ToArray();
    }

    private static void WriteCharacter(PacketWriter w, CharacterRecord c)
    {
        w.WriteUInt64(c.Id);        WriteWideString(w, c.Name);        w.WriteBits(c.Sex, 2);        w.WriteBits(c.Race, 5);        w.WriteBits(c.Class, 5);        w.WriteUInt32(c.Level);        w.WriteUInt32(c.FactionId);        w.WriteUInt32(0);        w.WriteUInt32(0);        w.WriteBits(0, 15);        w.WriteBits(0, 15);        w.WriteBits(0, 14);        w.WriteSingle(c.LocationX);
        w.WriteSingle(c.LocationY);
        w.WriteSingle(c.LocationZ);
        w.WriteSingle(0f);
        w.WriteSingle(0f);
        w.WriteBits(0, 3);        w.WriteBit(false);        w.WriteBit(false);        w.WriteUInt32(c.WorldId);        w.WriteBits(0, 4);        w.WriteUInt32(0);        w.WriteSingle(0f);    }

    private static void WriteWideString(PacketWriter w, string s)
    {
        int len = s?.Length ?? 0;
        if (len <= 0x7f)
        {
            w.WriteBit(false);            w.WriteBits((ulong)len, 7);
        }
        else
        {
            w.WriteBit(true);            w.WriteBits((ulong)len, 15);
        }
        if (s != null)
            foreach (char ch in s)
                w.WriteUInt16(ch);
    }
}
