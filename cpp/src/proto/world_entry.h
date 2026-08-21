#pragma once
#include <cstdint>
#include <vector>

// World-entry message serializers. Wire layouts are CLIENT-DERIVED (deser.py on the
// client's own Read functions; see spec/protocol/world-entry.md "CLIENT-DERIVED
// MESSAGE FORMATS"). Zero captures, zero NF. Generated from our DB per-character.
namespace nexus::proto {

struct WorldEntryTarget {
    uint64_t Guid = 0;      // the player's entity guid (character id)
    uint32_t WorldId = 0;   // target world (client already knows this from the char list)
    float X = 0.f, Y = 0.f, Z = 0.f;
    uint32_t Race = 0, Class = 0, Sex = 0, FactionId = 0;
};

class WorldEntryMessages {
public:
    // 0x0981 world-init: [u32 count][count x u32 id]
    static constexpr uint16_t OpWorldInit = 0x0981;
    static std::vector<uint8_t> BuildWorldInit(const std::vector<uint32_t>& ids);

    // 0x0988: [u32 n1][n1 x {wstr,wstr,u32,u32,u32,1b}][3b][u32 n2][n2 x {u32,wstr,u32,u32}]
    // First candidate sends both lists empty (the client-derived shape with zero entries).
    static constexpr uint16_t Op0988 = 0x0988;
    static std::vector<uint8_t> Build0988Empty();

    // 0x098B zone blob: [u32 count][...]; first candidate sends count 0.
    static constexpr uint16_t Op098B = 0x098B;
    static std::vector<uint8_t> Build098BEmpty();

    // 0x00AD — THE WORLD-ENTER response. Client handler sub_140022480 runs ONLY in
    // char-select state 4 (right after Enter Game/0x07DD): reads 6 u32s (worldId + 5 floats)
    // into +456..476, sets up the world connection with the pending char, and sets mgr
    // state 5 (LOADING). Wire (client Read sub_14007E9E0): [15 bits worldId][5 x float32]
    // (the 5 floats = X,Y,Z + 2, matching the char-list vec5 pattern). This is what makes
    // the client leave char-select and load the world at the spawn.
    static constexpr uint16_t OpWorldEnter = 0x00AD;
    static std::vector<uint8_t> BuildWorldEnter(uint32_t worldId, float x, float y, float z,
                                                float f4 = 0.f, float f5 = 0.f);

    // 0x0262 entity-create. Client Read WS+0x96FA0, 270 fixed bits + arrays. Minimal player
    // entity: guid + type + all arrays empty (cmdCount 0). The client fires PlayerChanged when
    // it matches an entity to the player it's tracking (guid read live from the client's 0x038C
    // movement). Full tree in spec/protocol/world-entry.md.
    static constexpr uint16_t OpEntityCreate = 0x0262;
    static std::vector<uint8_t> BuildPlayerEntityMinimal(uint32_t guid, uint32_t type);
    // Full entity WITH a position command (so it's placed in the world grid + lookup map).
    static std::vector<uint8_t> BuildPlayerEntity(uint32_t guid, float x, float y, float z);

    // 0x019B — SET PLAYER UNIT (char-select mgr variant, sub_1403B5AD0). Requires the entity to
    // already exist AND carry a +272 component; rejected as "foreign Message Id #411" on the world
    // channel. Superseded by 0x636 below. Kept for reference.
    static constexpr uint16_t OpSetPlayer = 0x019B;
    static std::vector<uint8_t> BuildSetPlayer(uint32_t guid, uint32_t field1 = 0);

    // 0x636 — THE world-channel SET PLAYER UNIT (client dispatch case 0x636 -> sub_14057A630).
    // Unlike 0x019B it has the expectedPlayer FALLBACK: if entity[guid] exists it binds it now;
    // if not, it stores guid at expectedPlayer(+25728) so the next 0x0262 entity-create with that
    // guid auto-binds as the player (client sub_1403D9760 line 928340) and fires PlayerChanged.
    // Wire (client Read sub_1400B09D0): [32b unitId][1b flag][32b playerGuid].
    static constexpr uint16_t OpSetPlayerUnit = 0x636;
    static std::vector<uint8_t> BuildSetPlayerUnit(uint32_t guid, bool flag = true);
};

} // namespace nexus::proto
