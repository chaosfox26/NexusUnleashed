#include "proto/world_entry.h"
#include "net/bitstream.h"

namespace nexus::proto {

using net::PacketWriter;

std::vector<uint8_t> WorldEntryMessages::BuildWorldInit(const std::vector<uint32_t>& ids) {
    PacketWriter w;
    w.WriteUInt32(static_cast<uint32_t>(ids.size()));
    for (uint32_t id : ids) w.WriteUInt32(id);
    return w.ToArray();
}

std::vector<uint8_t> WorldEntryMessages::Build0988Empty() {
    PacketWriter w;
    w.WriteUInt32(0);      // n1 = 0 (no {wstr,wstr,u32,u32,u32,1b} entries)
    w.WriteBits(0, 3);     // 3b field
    w.WriteUInt32(0);      // n2 = 0 (no {u32,wstr,u32,u32} entries)
    return w.ToArray();
}

std::vector<uint8_t> WorldEntryMessages::Build098BEmpty() {
    PacketWriter w;
    w.WriteUInt32(0);      // count = 0
    return w.ToArray();
}

std::vector<uint8_t> WorldEntryMessages::BuildWorldEnter(uint32_t worldId, float x, float y, float z,
                                                        float f4, float f5) {
    PacketWriter w;
    w.WriteBits(worldId & 0x7FFF, 15);   // worldId, 15 bits (client Read sub_14006C090 N=0xF)
    w.WriteSingle(x);                    // 5 floats (sub_1400AB810): X, Y, Z, then two more
    w.WriteSingle(y);
    w.WriteSingle(z);
    w.WriteSingle(f4);
    w.WriteSingle(f5);
    return w.ToArray();
}

std::vector<uint8_t> WorldEntryMessages::BuildPlayerEntityMinimal(uint32_t guid, uint32_t type) {
    // COMPLETE wire layout, EXACT from the client Read WS+0x96FA0 (341 bits = 43 bytes).
    // type=1 -> sub-reader sub_140080D30 (18b). All array counts 0. The 3 tail 2-bit selectors
    // -> sub_1400853F0 (1b each). Prior version stopped at bit 288 and the Read ran off the end
    // (0x800700E6). This is the full success path.
    (void)type;
    PacketWriter w;
    w.WriteUInt32(guid);          // a3+0    32b guid
    w.WriteBits(1, 6);            // a3+4    6b type=1
    w.WriteBits(0, 18);           // a3+8    18b (type-1 sub_140080D30)
    w.WriteBits(0, 8);            // a3+128  8b
    w.WriteBits(0, 5);            // a3+129  5b count1=0
    w.WriteUInt32(0);             // a3+144  32b
    w.WriteBits(0, 5);            // a3+148  5b count2=0
    w.WriteBits(0, 8);            // a3+160  8b count3=0
    w.WriteBits(0, 7);            // a3+176  7b count4=0 (visuals)
    w.WriteBits(0, 9);            // a3+192  9b count5=0 (commands/position)
    w.WriteUInt32(0);             // a3+208  32b
    w.WriteBits(0, 14);           // a3+212  14b
    w.WriteBits(0, 14);           // a3+216  14b
    w.WriteUInt32(0);             // a3+220  32b
    w.WriteUInt64(0);             // a3+224  64b
    w.WriteBits(0, 2);            // a3+232  2b selector1=0
    w.WriteBits(0, 1);            // a3+236  1b (sub_1400853F0)
    w.WriteBits(0, 2);            // a3+240  2b selector2=0
    w.WriteBits(0, 1);            // a3+248  1b
    w.WriteBits(0, 2);            // a3+264  2b selector3=0
    w.WriteBits(0, 1);            // a3+268  1b
    w.WriteBits(0, 14);           // a3+276  14b
    w.WriteBits(0, 17);           // a3+280  17b
    w.WriteBits(0, 15);           // a3+284  15b
    return w.ToArray();
}

std::vector<uint8_t> WorldEntryMessages::BuildPlayerEntity(uint32_t guid, float x, float y, float z) {
    // Entity kind 20 = "Player" (client type-name table): kind-20 reader sub_1400962D0 reads the
    // full character data block (218 bits, all strings/arrays empty here). This is what builds the
    // controllable-player component (+272) that set-player (0x019B) requires. Plus a position
    // command so the entity is placed in the world grid and is findable.
    PacketWriter w;
    // helper: client wide-string  [1b lenType][7b/15b len][len x u16]
    auto wstr = [&w](const std::u16string& s) {
        if (s.size() > 127) { w.WriteBit(true); w.WriteBits((uint32_t)s.size(), 15); }
        else { w.WriteBit(false); w.WriteBits((uint32_t)s.size(), 7); }
        for (char16_t c : s) w.WriteBits((uint16_t)c, 16);
    };
    w.WriteUInt32(guid);          // a3+0    32b guid
    w.WriteBits(20, 6);           // a3+4    6b type=20 (Player)
    // -- Player-kind block (sub_1400962D0), 218 bits --
    // The constructor (case 0x14, sub_140456960 line 1032717) REJECTS the entity unless
    //   struct+8  (this u64)  != 0   -> the player identity id, and
    //   struct+16 (this 14b)  != 0   -> the realm id.
    // Both zero was why the entity never landed in the lookup map (Read ok, construct FAIL).
    w.WriteUInt64(guid);          //   +0   u64  player id (non-zero)
    w.WriteBits(1, 14);           //   +8   14b  realm id = 1 (non-zero)
    wstr(u"Peryanna Meadowclover"); //  +16  player name
    w.WriteBits(0, 5);            //   +24  5b
    w.WriteBits(0, 5);            //   +28  5b
    w.WriteBits(0, 2);            //   +32  2b
    w.WriteUInt64(0);             //   +40  u64
    w.WriteBits(0, 8);            //   +48  count_a=0 (4-byte elems)
    wstr(u"");                    //   +64  string (empty)
    w.WriteBits(0, 4);            //   +72  4b
    w.WriteBits(0, 5);            //   +76  count_b=0 (8-byte elems)
    w.WriteBits(0, 6);            //   +88  count_c=0 (4-byte elems)
    w.WriteBits(0, 3);            //   +104 3b
    w.WriteBits(0, 8);            //   +108 8b
    w.WriteBits(0, 14);           //   +112 14b
    w.WriteBits(0, 8);            // a3+128  8b
    w.WriteBits(0, 5);            // a3+129  5b count1=0
    w.WriteUInt32(0);             // a3+144  32b
    w.WriteBits(0, 5);            // a3+148  5b count2=0
    w.WriteBits(0, 8);            // a3+160  8b count3=0
    w.WriteBits(0, 7);            // a3+176  7b count4=0 (visuals)
    w.WriteBits(1, 9);            // a3+192  9b command count = 1
    // -- command block (sub_140094BF0) --
    w.WriteSingle(x);             //   +0  32b posX
    w.WriteSingle(y);             //   +4  32b posY
    w.WriteSingle(z);             //   +8  32b posZ
    w.WriteBits(0, 18);           //   +C  18b
    w.WriteBits(0, 1);            //   +10 1b
    w.WriteUInt32(0);             //   +14 32b sub-count1 = 0
    w.WriteBits(0, 8);            //   +20 8b sub-count2 = 0
    w.WriteBits(0, 8);            //   +30 8b sub-count3 = 0
    // -- resume top-level --
    w.WriteUInt32(0);             // a3+208  32b
    // a3+212 / a3+216 are the entity's two faction fields (Faction1 / Faction2). The construction
    // common tail calls sub_14045AC60(entity, faction2@+216) which INSTALLS the entity+272 unit
    // component via sub_140716FA0 -- but ONLY when the value is a valid faction key. Zero was why
    // +272 stayed null and set-player failed (#411). 166 = Exiles Player faction (parent 165).
    w.WriteBits(166, 14);         // a3+212  14b Faction1 = Exiles Player
    w.WriteBits(166, 14);         // a3+216  14b Faction2 = Exiles Player -> installs entity+272
    w.WriteUInt32(0);             // a3+220  32b
    w.WriteUInt64(0);             // a3+224  64b
    w.WriteBits(0, 2);            // a3+232  2b sel1=0
    w.WriteBits(0, 1);            // a3+236  1b
    w.WriteBits(0, 2);            // a3+240  2b sel2=0
    w.WriteBits(0, 1);            // a3+248  1b
    w.WriteBits(0, 2);            // a3+264  2b sel3=0
    w.WriteBits(0, 1);            // a3+268  1b
    w.WriteBits(0, 14);           // a3+276  14b
    w.WriteBits(0, 17);           // a3+280  17b
    w.WriteBits(0, 15);           // a3+284  15b
    return w.ToArray();
}

std::vector<uint8_t> WorldEntryMessages::BuildSetPlayer(uint32_t guid, uint32_t field1) {
    PacketWriter w;
    w.WriteUInt32(guid);     // player unit guid (client looks it up)
    w.WriteUInt32(field1);
    return w.ToArray();
}

std::vector<uint8_t> WorldEntryMessages::BuildSetPlayerUnit(uint32_t guid, bool flag) {
    // 0x636 wire (client Read sub_1400B09D0): [32b unitId][1b flag][32b playerGuid].
    PacketWriter w;
    w.WriteBits(guid, 32);   // a2[0] unitId
    w.WriteBit(flag);        // a2[1] 1-bit flag (controlled-player)
    w.WriteBits(guid, 32);   // a2[2] playerGuid -> expectedPlayer / bind target
    return w.ToArray();
}

} // namespace nexus::proto
