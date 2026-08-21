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
    w.WriteBits(4, 5);            //   +24  5b  race = 4 (Aurin)   [DB character.race]
    w.WriteBits(7, 5);            //   +28  5b  class = 7          [DB character.class]
    w.WriteBits(1, 2);            //   +32  2b  sex = 1 (female)   [DB character.sex]
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
    // a3+148 = the MOVEMENT array (count 5b, elements sub_1400AF930). Construction applies THIS
    // array (not the 64-byte command array) via sub_1404586E0 -> the spline interpolator, seeding
    // the entity's initial transform (+4576). This is the channel that actually places the entity.
    // Element type 2 (funcs_1400AF98E[2]=sub_1400AD350) = position keyframe: [3x 32b float][1b].
    w.WriteBits(1, 5);            // a3+148  5b movement count = 1
    // ---- movement element (sub_1400AF930): [5b type][type data] ----
    w.WriteBits(2, 5);            //   5b type = 2 (position keyframe)
    w.WriteSingle(x);             //   32b posX (sub_14006C1C0)
    w.WriteSingle(y);             //   32b posY
    w.WriteSingle(z);             //   32b posZ
    w.WriteBits(0, 1);            //   1b
    w.WriteBits(0, 8);            // a3+160  8b count3=0
    // a3+176 = ITEM-VISUAL array (count 7b, element sub_1400AB890 = [7b slot][15b displayId][14b][32b],
    // the same wire format as the char-list appearance that renders her on the select screen). Populated
    // from character_appearance (slot -> displayId) so her body/clothing renders instead of a floating
    // head. Peryanna's 7 slots (id 32); TODO: make this a per-character parameter from the DB.
    static const struct { uint16_t slot; uint16_t display; } kVis[] = {
        {24, 4928}, {25, 5734}, {26, 6279}, {27, 5953}, {28, 5691}, {39, 6626}, {70, 7277}
    };
    w.WriteBits((uint32_t)(sizeof(kVis) / sizeof(kVis[0])), 7);   // a3+176 count = 7 visuals
    for (const auto& v : kVis) {
        w.WriteBits(v.slot, 7);       // [7b]  item slot
        w.WriteBits(v.display, 15);   // [15b] item displayId
        w.WriteBits(0, 14);           // [14b]
        w.WriteUInt32(0);             // [32b]
    }
    w.WriteBits(0, 9);            // a3+192  9b command count = 0 (position not carried here)
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

std::vector<uint8_t> WorldEntryMessages::BuildWorldChangeDone(uint8_t status) {
    // 0x36A wire (client Read sub_14007E950): a single 5-bit status. 0 = success -> render game.
    PacketWriter w;
    w.WriteBits(status & 0x1F, 5);
    return w.ToArray();
}

std::vector<uint8_t> WorldEntryMessages::BuildLoadProgress(uint32_t current, uint32_t field1, uint32_t max) {
    // 0x845 loading progress (client handler WorldPaketHandler case 0x845): a4[0]=current,
    // a4[1]=field1, a4[2]=max -> fills the load bar (a1+29376 current / a1+29384 max). Also
    // serves as world-channel keepalive traffic while the client is in its load state.
    PacketWriter w;
    w.WriteUInt32(current);
    w.WriteUInt32(field1);
    w.WriteUInt32(max);
    return w.ToArray();
}

std::vector<uint8_t> WorldEntryMessages::BuildLoadScreenState(uint8_t state) {
    // 0x3D0 wire (client Read sub_14007FDC0): a single 3-bit state value.
    PacketWriter w;
    w.WriteBits(state & 0x7, 3);
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
