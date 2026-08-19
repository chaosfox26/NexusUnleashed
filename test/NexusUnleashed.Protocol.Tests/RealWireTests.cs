// Validate the clean-room PacketReader against REAL captured WildStar bytes.
// These hex strings are pre-encryption message payloads (opcode + body) captured
// from the oracle's wire (server->client 0x0935 movement). If our reader pulls
// the right opcode + guid out of them, our bit codec matches Carbine's wire.
using System;
using System.Globalization;
using NexusUnleashed.Network;

int pass = 0, fail = 0;
void Check(string n, bool ok, string d = "") { if (ok) { pass++; Console.WriteLine($"  PASS {n} {d}"); } else { fail++; Console.WriteLine($"  FAIL {n} {d}"); } }

byte[] Hex(string h) { var b = new byte[h.Length/2]; for (int i=0;i<b.Length;i++) b[i]=byte.Parse(h.Substring(i*2,2),NumberStyles.HexNumber); return b; }

// real 0x0935 samples (guid should be constant 5076 across all - same tracked entity)
string[] samples = {
    "3509d41300000200b07d08",
    "3509d41300000200407f08",
    "3509d4130000425c367f08",
    "3509d4130000824a0f7f08",
    "3509d413000062f8f17e08",
};

uint firstGuid = 0; bool guidConstant = true;
for (int i = 0; i < samples.Length; i++)
{
    var r = new PacketReader(Hex(samples[i]));
    ushort opcode = (ushort)r.ReadBits(16);
    uint guid = r.ReadUInt32();
    if (i == 0) { Check("opcode reads as 0x0935", opcode == 0x0935, $"(0x{opcode:X4})"); firstGuid = guid; }
    Check($"sample {i}: opcode 0x0935", opcode == 0x0935);
    if (guid != firstGuid) guidConstant = false;
    Console.WriteLine($"    sample {i}: opcode=0x{opcode:X4} guid={guid}");
}
Check("guid is constant across the movement stream (5076)", guidConstant && firstGuid == 5076, $"({firstGuid})");


// --- message model validation against real captured payloads ---
Console.WriteLine("-- message models vs real bytes --");
{
    var m = ServerEntitySmallUpdate.Parse(Hex("55032c07000001"));
    Check("0x0355 small-update: guid+flag", m.Guid == 1836 && m.Flag == 1, $"(guid={m.Guid} flag={m.Flag})");

    var add = ServerSpellBuffAdd.Parse(Hex("11080300000001000000d4130000"));
    Check("0x0811 buff-add: buffId+count+target", add.BuffId == 3 && add.Count == 1 && add.TargetGuid == 5076, $"(buff={add.BuffId} count={add.Count} target={add.TargetGuid})");

    var rem = ServerSpellBuffRemove.Parse(Hex("130803000000d4130000"));
    Check("0x0813 buff-remove: buffId+target", rem.BuffId == 3 && rem.TargetGuid == 5076, $"(buff={rem.BuffId} target={rem.TargetGuid})");

    var upd = ServerEntityUpdate.Parse(Hex("3809d41300000f00000000"));
    Check("0x0938 entity-update: guid+fields", upd.Guid == 5076 && upd.FieldA == 15 && upd.Tail == 0, $"(guid={upd.Guid} a={upd.FieldA} b={upd.FieldB})");

    var pos = ServerEntityPositionUpdate.Parse(Hex("3509d41300000200b07d08"));
    Check("0x0935 position: guid extracted", pos.Guid == 5076, $"(guid={pos.Guid} move=0x{pos.MovementData:X8})");
}


// 0x0262 entity-create header (guid pinned; body bit-packed, pending)
{
    var ec = ServerEntityCreate.Parse(Hex("6202f0070000c04c2d0000431100000000a0010000a0d000000000000000a000000000206666928835b3ce8835632a8a29"));
    Check("0x0262 entity-create: guid + position", ec.Guid == 2032 && Math.Abs(ec.Y - (-925.4f)) < 0.5f, $"(guid={ec.Guid} pos=({ec.X:F0},{ec.Y:F0},{ec.Z:F0}))");
}


// batch 2: relation / value / counter
{
    var rel = ServerEntityRelation.Parse(Hex("7608bf070000d4130000"));
    Check("0x0876 relation: source+target guids", rel.SourceGuid == 1983 && rel.TargetGuid == 5076, $"({rel.SourceGuid}->{rel.TargetGuid})");
    var val = ServerEntityValue.Parse(Hex("2f09d4130000b0040000000000"));
    Check("0x092F value: guid + value 1200", val.Guid == 5076 && val.Value == 1200, $"(guid={val.Guid} val={val.Value})");
    var cnt = ServerCounter.Parse(Hex("fe0704000000"));
    Check("0x07FE counter: single u32", cnt.Value == 4, $"({cnt.Value})");
}


// entity-create POSITION decode (bit offset 289 = 3x float32), cracked by the
// bit-shift search and cross-checked against the operator's world coords.
{
    byte[] p = Hex("6202f0070000c04c2d0000431100000000a0010000a0d000000000000000a000000000206666928835b3ce8835632a8a29");
    var r = new PacketReader(p);
    Check("entity-create opcode", (ushort)r.ReadBits(16) == 0x0262);
    uint guid = r.ReadUInt32();
    Check("entity-create guid 2032", guid == 2032, $"({guid})");
    int skip = 289 - 48;                   // skip to the position field
    while (skip > 0) { int c = Math.Min(32, skip); r.ReadBits(c); skip -= c; }
    float x = r.ReadSingle(), y = r.ReadSingle(), z = r.ReadSingle();
    Console.WriteLine($"    decoded position: ({x:F2}, {y:F2}, {z:F2})");
    bool ok = Math.Abs(x - (-804.80f)) < 0.5f && Math.Abs(y - (-925.40f)) < 0.5f && Math.Abs(z - (-2387.10f)) < 0.5f;
    Check("entity-create POSITION decodes to real world coords", ok);
}

Console.WriteLine($"{pass} pass / {fail} fail");
return fail == 0 ? 0 : 1;
