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

// --- encrypted world container round-trip vs the REAL captured ServerHello ---
// The first server->client 0x03DC frame of the login capture. The container
// payload is [u32 innerLen][encrypted inner]; decrypting yields inner opcode
// 0x0003 (AuthHello) and its body must match the decrypted 0x0003 seen in the
// other capture. Encoding it back must reproduce the exact captured wire bytes.
Console.WriteLine("-- encrypted world container (real ServerHello) --");
{
    const ulong seed = 0xD283F5B34A8DC685ul;
    // container payload as logged (starts at the innerLen field):
    byte[] payload = Hex("350000001a57c0cbff79ba9c87080349bf63806df50021ea5e4a2918faa344ca85401d094f69e88d7762748aee15966790e91be068");
    var crypt = new NexusUnleashed.Cryptography.PacketCrypt(seed);
    var (op, body) = NexusUnleashed.Network.WorldPacket.DecodeContainer(payload, crypt);
    Check("container decodes to inner opcode 0x0003", op == 0x0003, $"(0x{op:X4})");
    string bodyHex = Convert.ToHexString(body).ToLowerInvariant();
    string expectBody = "aa3e0000010000001500000000000000000000000000000000000b14332f0100000000000000000000000000000000";
    Check("container inner body == decrypted 0x0003 body", bodyHex == expectBody);

    // encode the inner back into a full frame and compare to the captured wire.
    // full outer frame = [u32 size=0x3b][u16 0x03DC][payload] (frame.md capture).
    var crypt2 = new NexusUnleashed.Cryptography.PacketCrypt(seed);
    byte[] frame = NexusUnleashed.Network.WorldPacket.EncodeServer(op, body, crypt2);
    string frameHex = Convert.ToHexString(frame).ToLowerInvariant();
    string expectFrame = "3b000000dc03" + Convert.ToHexString(payload).ToLowerInvariant();
    Check("EncodeServer reproduces the captured ServerHello wire byte-for-byte", frameHex == expectFrame, frameHex == expectFrame ? "" : $"\n      got {frameHex}\n      exp {expectFrame}");
}

// --- 0x0981 world-init id list round-trip vs REAL captured bytes ---
Console.WriteLine("-- world-init 0x0981 (real captured world-entry bytes) --");
{
    byte[] real = Hex("8109fb0000000100000002000000030000000400000005000000060000000700000008000000090000000a0000000b0000000c0000000d0000000e0000000f000000100000001100000012000000130000001400000015000000160000001700000018000000190000001a0000001b0000001c0000001d0000001e0000001f000000200000002100000022000000230000002400000025000000260000002700000028000000290000002a0000002b0000002c0000002d0000002e0000002f000000300000003100000032000000330000003400000035000000360000003700000038000000390000003a0000003b0000003c0000003d0000003e0000003f000000400000004100000042000000430000004400000045000000460000004700000048000000490000004a0000004b0000004c0000004d0000004e0000004f000000500000005100000052000000530000005400000055000000560000005700000058000000590000005a0000005b0000005c0000005d0000005e0000005f000000600000006100000062000000630000006400000065000000660000006700000068000000690000006a0000006b0000006c0000006d0000006e0000006f000000700000007100000072000000730000007400000075000000760000007700000078000000790000007a0000007b0000007c0000007d0000007e0000007f000000800000008100000082000000830000008400000085000000860000008700000088000000890000008a0000008b0000008c0000008d0000008e0000008f000000900000009100000092000000930000009400000095000000960000009700000098000000990000009a0000009b0000009c0000009d0000009e0000009f000000a0000000a1000000a2000000a3000000a4000000a5000000a6000000a7000000a8000000a9000000aa000000ab000000ac000000ad000000ae000000af000000b0000000b1000000b2000000b3000000b4000000b5000000b6000000b7000000b8000000b9000000ba000000bb000000bc000000bd000000be000000bf000000c0000000c1000000c2000000c3000000c4000000c5000000c6000000c7000000c8000000c9000000ca000000cb000000cc000000cd000000ce000000cf000000d0000000d1000000d2000000d3000000d4000000d5000000d6000000d7000000d8000000d9000000da000000db000000dc000000dd000000de000000df000000e0000000e1000000e2000000e3000000e4000000e5000000e6000000e7000000e8000000e9000000ea000000eb000000ec000000ed000000ee000000ef000000f0000000f1000000f2000000f3000000f5000000f6000000f7000000f8000000f9000000fa000000fb000000fc000000");
    var wi = NexusUnleashed.Network.ServerWorldInit.Parse(real);
    Check("0x0981 parses 251 ids", wi.Ids.Length == 251, $"({wi.Ids.Length})");
    Check("0x0981 first ids 1,2,3", wi.Ids.Length >= 3 && wi.Ids[0] == 1 && wi.Ids[1] == 2 && wi.Ids[2] == 3);
    byte[] rebuilt = wi.Build();
    Check("0x0981 Build reproduces the captured world-init bytes byte-for-byte",
        Convert.ToHexString(rebuilt).ToLowerInvariant() == Convert.ToHexString(real).ToLowerInvariant(),
        rebuilt.Length == real.Length ? "" : $"(len {rebuilt.Length} vs {real.Length})");
}

// --- TWO-PHASE KEY: decrypt a real WORLD-key message end to end ---
// Auth key (0xD283F5B34A8DC685) handles the hello; after login the channel
// re-keys to GetKeyFromTicket(sessionKey). This wrapper is a real captured
// world message under that world key (recovered keyInteger 0x4888DCE5CA507060);
// our WorldPacket + PacketCrypt(worldKey) decrypt it to 0x0981 + the 251-id list.
Console.WriteLine("-- two-phase keying: real WORLD-key decrypt --");
{
    Check("AuthChannelKey == 0xD283F5B34A8DC685 (observed on the wire)",
        NexusUnleashed.Cryptography.PacketCrypt.AuthChannelKey == 0xD283F5B34A8DC685ul);
    var worldCrypt = new NexusUnleashed.Cryptography.PacketCrypt(0x4888DCE5CA507060ul);
    byte[] wrapperPayload = Hex("f603000099a9bf68ebb004b11840d11cf72b3ae89163af6f107e62014081f42c6cf19a1e199875a5678c7b913811b89454c76f3211558cbdbc886f666079d89e7ce182cf990a84805900b6095816f578eb17022d916ece7648107e5a80c4c4b3ff0e757999a425229872d94378195b86c9d899a5918fc53750e7663ea0c24ad40ef084c119507182a774d33698b93ff6bbefcd6f119a61855cbab586c0781ac620356d999961bb4f2b48ac16b8e8567e180398b591ac4257f04cb8e1e08036743025754819f34a6a15c4618ed8ef1b92a7d3f5aa1197009c04d4a9dd003d2a59b3ca82fe195debc8d4b60ec4f8e0b56c851c6e2211760bdd1c23b1b9203ba43e4234734699a9ff68ebb044b11840d11cf72b3ae89163ef6f107e22014081f42c6cf19a1e199835a5678c3b913811b89454c76f321155ccbdbc882f666079d89e7ce182cf990ac4805900f6095816f578eb17022d916e8e7648103e5a80c4c4b3ff0e757999a465229872994378195b86c9d899a5918f853750e7263ea0c24ad40ef084c119503182a774933698b93ff6bbefcd6f119a21855cbaf586c0781ac620356d999961fb4f2b48ec16b8e8567e180398b591ac0257f04cf8e1e08036743025754819f30a6a15c4218ed8ef1b92a7d3f5aa1197409c04d4e9dd003d2a59b3ca82fe195dabc8d4b64ec4f8e0b56c851c6e2211764bdd1c23f1b9203ba43e4234734699a93f68ebb084b11840d11cf72b3ae891632f6f107ee2014081f42c6cf19a1e1998f5a5678cfb913811b89454c76f3211550cbdbc88ef666079d89e7ce182cf990a0480590036095816f578eb17022d916e4e764810fe5a80c4c4b3ff0e757999a4a5229872594378195b86c9d899a5918f453750e7e63ea0c24ad40ef084c11950f182a774533698b93ff6bbefcd6f119ae1855cba3586c0781ac620356d9999613b4f2b482c16b8e8567e180398b591acc257f04c38e1e08036743025754819f3ca6a15c4e18ed8ef1b92a7d3f5aa1197809c04d429dd003d2a59b3ca82fe195d6bc8d4b68ec4f8e0b56c851c6e2211768bdd1c2331b9203ba43e4234734699a97f68ebb0c4b11840d11cf72b3ae891636f6f107ea2014081f42c6cf19a1e1998b5a5678cbb913811b89454c76f3211554cbdbc88af666079d89e7ce182cf990a4480590076095816f578eb17022d916e0e764810be5a80c4c4b3ff0e757999a4e5229872194378195b86c9d899a5918f053750e7a63ea0c24ad40ef084c11950b182a774133698b93ff6bbefcd6f119aa1855cba7586c0781ac620356d9999617b4f2b486c16b8e8567e180398b591ac8257f04c78e1e08036743025754819f38a6a15c4a18ed8ef1b92a7d3f5aa1197c19c04d46add003d2a59b3ca8efe195d2ac8d4b6c1c4f8e0b56c851c66221176");
    var (wop, wbody) = NexusUnleashed.Network.WorldPacket.DecodeContainer(wrapperPayload, worldCrypt);
    Check("world-key container decodes to inner opcode 0x0981", wop == 0x0981, $"(0x{wop:X4})");
    // rebuild full payload (op+body) and parse the id list
    var full = new byte[wbody.Length + 2];
    full[0] = 0x81; full[1] = 0x09; System.Array.Copy(wbody, 0, full, 2, wbody.Length);
    var wi2 = NexusUnleashed.Network.ServerWorldInit.Parse(full);
    Check("world-key 0x0981 decrypts to the 251-id list", wi2.Ids.Length == 251 && wi2.Ids[0] == 1, $"({wi2.Ids.Length})");
}

Console.WriteLine($"{pass} pass / {fail} fail");
return fail == 0 ? 0 : 1;
