#include "proto/world_entry.h"
#include "net/bitstream.h"

namespace nexus::proto {

using net::PacketWriter;

std::vector<uint8_t> WorldEntryMessages::BuildWorldEnter(uint32_t worldId, float x, float y, float z,
                                                        float f4, float f5) {
    PacketWriter w;
    w.WriteBits(worldId & 0x7FFF, 15);
    w.WriteSingle(x);
    w.WriteSingle(y);
    w.WriteSingle(z);
    w.WriteSingle(f4);
    w.WriteSingle(f5);
    return w.ToArray();
}

std::vector<uint8_t> WorldEntryMessages::BuildPlayerEntity(uint32_t guid, float x, float y, float z,
                                                          const PlayerAppearance& appearance) {
    PacketWriter w;
    auto wstr = [&w](const std::u16string& s) {
        if (s.size() > 127) { w.WriteBit(true); w.WriteBits((uint32_t)s.size(), 15); }
        else { w.WriteBit(false); w.WriteBits((uint32_t)s.size(), 7); }
        for (char16_t c : s) w.WriteBits((uint16_t)c, 16);
    };
    w.WriteUInt32(guid);
    w.WriteBits(20, 6);                        // entity kind 20 = Player
    // Player-kind block
    w.WriteUInt64(guid);                       // player id (non-zero)
    w.WriteBits(1, 14);                        // realm id (non-zero)
    wstr(appearance.Name);
    w.WriteBits(appearance.Race & 0x1F, 5);
    w.WriteBits(appearance.Class & 0x1F, 5);
    w.WriteBits(appearance.Sex & 0x3, 2);
    w.WriteUInt64(0);
    w.WriteBits(0, 8);
    wstr(u"");
    w.WriteBits(0, 4);
    w.WriteBits(0, 5);
    w.WriteBits(0, 6);
    w.WriteBits(0, 3);
    w.WriteBits(0, 8);
    w.WriteBits(0, 14);
    w.WriteBits(0, 8);
    // unit-property array: 1 entry, id 12 = Health, type 2 = {current, max}
    w.WriteBits(1, 5);
    w.WriteBits(12, 5);
    w.WriteBits(2, 2);
    w.WriteUInt32(250);
    w.WriteUInt32(250);
    w.WriteUInt32(0);
    // movement array: 1 position keyframe (places the entity)
    w.WriteBits(1, 5);
    w.WriteBits(2, 5);
    w.WriteSingle(x);
    w.WriteSingle(y);
    w.WriteSingle(z);
    w.WriteBits(0, 1);
    w.WriteBits(0, 8);
    // item-visual array: [7b slot][15b displayId][14b][32b] per character_appearance row
    w.WriteBits((uint32_t)(appearance.Visuals.size() & 0x7F), 7);
    for (const auto& v : appearance.Visuals) {
        w.WriteBits(v.first & 0x7F, 7);
        w.WriteBits(v.second & 0x7FFF, 15);
        w.WriteBits(0, 14);
        w.WriteUInt32(0);
    }
    w.WriteBits(0, 9);
    w.WriteUInt32(0);
    w.WriteBits(appearance.Faction & 0x3FFF, 14);   // Faction1
    w.WriteBits(appearance.Faction & 0x3FFF, 14);   // Faction2 (installs the unit component)
    w.WriteUInt32(0);
    w.WriteUInt64(0);
    w.WriteBits(0, 2);
    w.WriteBits(0, 1);
    w.WriteBits(0, 2);
    w.WriteBits(0, 1);
    w.WriteBits(0, 2);
    w.WriteBits(0, 1);
    w.WriteBits(0, 14);
    w.WriteBits(0, 17);
    w.WriteBits(0, 15);
    return w.ToArray();
}

std::vector<uint8_t> WorldEntryMessages::BuildSetPlayer(uint32_t guid, uint32_t field1) {
    PacketWriter w;
    w.WriteUInt32(guid);
    w.WriteUInt32(field1);
    return w.ToArray();
}

std::vector<uint8_t> WorldEntryMessages::BuildWorldChangeDone(uint8_t status) {
    PacketWriter w;
    w.WriteBits(status & 0x1F, 5);   // 0 = success
    return w.ToArray();
}

std::vector<uint8_t> WorldEntryMessages::BuildLoadProgress(uint32_t current, uint32_t field1, uint32_t max) {
    PacketWriter w;
    w.WriteUInt32(current);
    w.WriteUInt32(field1);
    w.WriteUInt32(max);
    return w.ToArray();
}

std::vector<uint8_t> WorldEntryMessages::BuildCharacterDataMinimal() {
    // Empty-but-valid character blob; fires CharacterCreated. Field order per the client reader.
    PacketWriter w;
    w.WriteBits(0, 32);
    for (int i = 0; i < 120; ++i) w.WriteBits(0, 8);
    w.WriteUInt64(0);
    w.WriteBits(0, 32);
    w.WriteBits(0, 32);
    w.WriteBits(0, 32);
    w.WriteBits(0, 32);
    w.WriteBits(0, 32);
    w.WriteBits(1, 3);                  // path type (1 = Soldier)
    w.WriteBits(0, 16);
    w.WriteBits(0, 32);
    w.WriteBits(0, 14);
    w.WriteBits(0, 16);
    w.WriteBits(0, 32);
    w.WriteBits(0, 32);
    w.WriteBits(0, 16);
    w.WriteBits(0, 32);
    w.WriteBits(0, 32);
    w.WriteBits(0, 32);
    w.WriteBits(0, 6);
    for (int i = 0; i < 1024; ++i) w.WriteBits(0, 8);
    w.WriteSingle(0.0f);
    w.WriteBits(0, 1);
    w.WriteBits(0, 32);
    w.WriteBits(0, 32);
    return w.ToArray();
}

std::vector<uint8_t> WorldEntryMessages::BuildSetPlayerPath(uint8_t pathType) {
    PacketWriter w;
    w.WriteBits(pathType & 0x7, 3);
    for (int i = 0; i < 16; ++i) w.WriteBits(0, 8);
    w.WriteBits(0, 4);
    w.WriteSingle(0.0f);
    return w.ToArray();
}

std::vector<uint8_t> WorldEntryMessages::BuildSetPlayerUnit(uint32_t guid, bool flag) {
    PacketWriter w;
    w.WriteBits(guid, 32);   // unitId
    w.WriteBit(flag);
    w.WriteBits(guid, 32);   // playerGuid (expectedPlayer / bind target)
    return w.ToArray();
}

std::vector<uint8_t> WorldEntryMessages::BuildItemAdd(uint64_t itemGuid, uint32_t itemId,
                                                     uint16_t locationType, uint32_t slotIndex,
                                                     uint32_t stackCount, float durability) {
    // Item struct + 6b tail, field order per the client reader. Item renders from Item2.tbl by itemId.
    PacketWriter w;
    w.WriteUInt64(itemGuid);
    w.WriteUInt64(0);
    w.WriteBits(itemId & 0x3FFFF, 18);    // itemId
    w.WriteBits(locationType & 0x1FF, 9); // location type (0 = equipped)
    w.WriteUInt32(slotIndex);             // slot (16 = weapon)
    w.WriteUInt32(stackCount);
    w.WriteUInt32(0);
    w.WriteUInt64(0);
    w.WriteUInt32(0);
    w.WriteUInt64(0);
    w.WriteSingle(durability);
    w.WriteUInt32(0);
    w.WriteBits(0, 8);
    w.WriteUInt32(0);
    w.WriteUInt32(0);
    w.WriteUInt32(0);
    for (int i = 0; i < 2; ++i) {         // rune/glyph slots
        w.WriteBits(0, 3);
        w.WriteUInt32(0);
        w.WriteUInt32(0);
    }
    w.WriteBits(0, 18);
    w.WriteBits(0, 3);                    // countA
    w.WriteBits(0, 4);                    // countB
    w.WriteBits(0, 6);                    // countC
    w.WriteUInt32(0);
    w.WriteBits(0, 6);                    // tail
    return w.ToArray();
}

} // namespace nexus::proto
