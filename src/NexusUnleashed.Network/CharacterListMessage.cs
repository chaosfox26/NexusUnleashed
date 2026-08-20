// NexusUnleashed - clean-room authored. Serializer for the character-list message
// (inner opcode 0x0117, server->client). The wire layout is a FACT read out of the
// real 16042 client's own deserializer (WS+0x7FAB0 top-level, WS+0x7F720 per-char),
// observed read-only via Frida — no NF source, no NF captures. Full field map and
// method: spec/protocol/char-list-0x117.md. Bits are LSB-first (PacketWriter),
// matching the client's bit reader (loads LE u64, shr by bit-pos, mask).
using System.Collections.Generic;

namespace NexusUnleashed.Network;

/// <summary>One character row as the client's char-list deserializer expects it.</summary>
public sealed class CharacterRecord
{
    public ulong Id;
    public string Name = "";
    public uint Sex;        // 2 bits
    public uint Race;       // 5 bits
    public uint Class;      // 5 bits
    public uint Level;      // carried in a +0x1c u32 (INFERRED slot)
    public uint FactionId;  // carried in a +0x20 u32 (INFERRED slot)
    public float LocationX;
    public float LocationY;
    public float LocationZ;
    public uint WorldId;
}

/// <summary>Builds the 0x0117 inner body (opcode is prepended by the framing layer).</summary>
public static class CharacterListMessage
{
    public const ushort Opcode = 0x0117;

    public static byte[] Build(IReadOnlyList<CharacterRecord> characters)
    {
        var w = new PacketWriter();

        // ---- top-level struct (WS+0x7FAB0) ----
        w.WriteUInt64(0);                       // +0x00 header (INFERRED) — server/realm id
        w.WriteUInt32((uint)characters.Count);  // +0x08 character count

        foreach (var c in characters)
            WriteCharacter(w, c);

        // +0x18 count2 array (empty), +0x28 count3 array (empty)
        w.WriteUInt32(0);                       // +0x18 count2
        w.WriteUInt32(0);                       // +0x28 count3
        w.WriteBits(0, 14);                     // +0x38
        w.WriteBits(0, 14); w.WriteUInt64(0);   // +0x40 {14b, u64}  (WS+0x852F0)
        w.WriteUInt32(0);                       // +0x50
        w.WriteUInt32(0);                       // +0x54
        w.WriteUInt32(0);                       // +0x58
        w.WriteUInt32(0);                       // +0x5c
        w.WriteBits(0, 14);                     // +0x60
        w.WriteBit(false);                      // +0x64 (1 bit)

        return w.ToArray();
    }

    // Per-character record (WS+0x7F720), 0xA0 bytes in the client's struct.
    private static void WriteCharacter(PacketWriter w, CharacterRecord c)
    {
        w.WriteUInt64(c.Id);                    // +0x00 character id
        WriteWideString(w, c.Name);             // +0x08 name (WS+0x336A40)
        w.WriteBits(c.Sex, 2);                  // +0x10
        w.WriteBits(c.Race, 5);                 // +0x14
        w.WriteBits(c.Class, 5);                // +0x18
        w.WriteUInt32(c.Level);                 // +0x1c  (INFERRED slot)
        w.WriteUInt32(c.FactionId);             // +0x20  (INFERRED slot)
        w.WriteUInt32(0);                       // +0x24 countA (appearance list) — empty
        w.WriteUInt32(0);                       // +0x30 countB (appearance list) — empty
        w.WriteBits(0, 15);                     // +0x40
        w.WriteBits(0, 15);                     // +0x44
        w.WriteBits(0, 14);                     // +0x48
        // +0x4c: FIVE floats (WS+0xAB810 reads 5, confirmed by the client's bit trace).
        w.WriteSingle(c.LocationX);
        w.WriteSingle(c.LocationY);
        w.WriteSingle(c.LocationZ);
        w.WriteSingle(0f);
        w.WriteSingle(0f);
        w.WriteBits(0, 3);                      // +0x60
        w.WriteBit(false);                      // +0x64
        w.WriteBit(false);                      // +0x68
        w.WriteUInt32(c.WorldId);               // +0x6c (INFERRED slot)
        w.WriteBits(0, 4);                      // +0x70 countC — empty (two u32 arrays follow, both 0)
        w.WriteUInt32(0);                       // +0x88 countD — empty
        w.WriteSingle(0f);                      // +0x98 float (WS+0x6C1C0)
    }

    // Wide-string wire form (WS+0x336A40): [1b lenType][lenType==0 ? 7b len : 15b len][len × u16].
    private static void WriteWideString(PacketWriter w, string s)
    {
        int len = s?.Length ?? 0;
        if (len <= 0x7f)
        {
            w.WriteBit(false);          // 7-bit length variant
            w.WriteBits((ulong)len, 7);
        }
        else
        {
            w.WriteBit(true);           // 15-bit length variant
            w.WriteBits((ulong)len, 15);
        }
        if (s != null)
            foreach (char ch in s)
                w.WriteUInt16(ch);
    }
}
