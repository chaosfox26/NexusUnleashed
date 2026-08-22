#pragma once
#include <cstdint>
#include <string>
#include <utility>
#include <vector>

// World-entry message serializers. Wire layouts are client-derived; generated per-character from
// the DB. See cpp/docs/CODE-NOTES.md "World entry" for the format/reference details.
namespace nexus::proto {

// Per-character body the 0x0262 player entity renders. Race/class/sex from `character`; Visuals =
// {slot, displayId} rows from character_appearance. Faction is the entity-construction key (166),
// NOT the DB factionId.
struct PlayerAppearance {
    uint32_t Race = 4;
    uint32_t Class = 7;
    uint32_t Sex = 1;
    uint32_t Faction = 166;
    std::u16string Name = u"Peryanna Meadowclover";
    std::vector<std::pair<uint16_t, uint16_t>> Visuals;
};

class WorldEntryMessages {
public:
    // 0x00AD world-enter: [15b worldId][5 x f32].
    static constexpr uint16_t OpWorldEnter = 0x00AD;
    static std::vector<uint8_t> BuildWorldEnter(uint32_t worldId, float x, float y, float z,
                                                float f4 = 0.f, float f5 = 0.f);

    // 0x0262 entity-create (kind 20 = Player): identity + race/sex/class/name, Health property,
    // a position keyframe, item-visuals, and faction (installs the unit component).
    static constexpr uint16_t OpEntityCreate = 0x0262;
    static std::vector<uint8_t> BuildPlayerEntity(uint32_t guid, float x, float y, float z,
                                                  const PlayerAppearance& appearance);

    // 0x019B set-player.
    static constexpr uint16_t OpSetPlayer = 0x019B;
    static std::vector<uint8_t> BuildSetPlayer(uint32_t guid, uint32_t field1 = 0);

    // 0x0845 loading progress / world-channel keepalive: [u32 current][u32 field1][u32 max].
    static constexpr uint16_t OpLoadProgress = 0x0845;
    static std::vector<uint8_t> BuildLoadProgress(uint32_t current, uint32_t field1, uint32_t max);

    // 0x025E character-data blob -> fires CharacterCreated.
    static constexpr uint16_t OpCharacterData = 0x025E;
    static std::vector<uint8_t> BuildCharacterDataMinimal();

    // 0x06BC set player path: [3b pathType][16 bytes][4b][f32] (1 Soldier / 2 Settler / 3 Scientist / 4 Explorer).
    static constexpr uint16_t OpSetPlayerPath = 0x06BC;
    static std::vector<uint8_t> BuildSetPlayerPath(uint8_t pathType);

    // 0x111 item add: guid + itemId + location{type,slot} + instance state.
    static constexpr uint16_t OpItemAdd = 0x111;
    static std::vector<uint8_t> BuildItemAdd(uint64_t itemGuid, uint32_t itemId,
                                             uint16_t locationType, uint32_t slotIndex,
                                             uint32_t stackCount = 1, float durability = 1.0f);

    // 0x636 set-player-unit: [32b unitId][1b flag][32b playerGuid]. Sent before 0x0262 so the
    // expectedPlayer fallback auto-binds the created entity as the player.
    static constexpr uint16_t OpSetPlayerUnit = 0x636;
    static std::vector<uint8_t> BuildSetPlayerUnit(uint32_t guid, bool flag = true);

    // 0x93C set-stand-state: [u32 guid][u32 standState][u32 stateData]. State 0 = Stand, 2 = LyingDown.
    static constexpr uint16_t OpSetStandState = 0x93C;
    static std::vector<uint8_t> BuildStandState(uint32_t guid, uint32_t standState = 0,
                                                uint32_t stateData = 0);
};

} // namespace nexus::proto
