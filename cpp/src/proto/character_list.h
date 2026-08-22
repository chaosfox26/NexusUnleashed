#pragma once
#include <cstdint>
#include <string>
#include <utility>
#include <vector>

namespace nexus::proto {

struct CharacterRecord {
    uint64_t Id = 0;
    std::string Name;
    uint32_t Sex = 0;
    uint32_t Race = 0;
    uint32_t Class = 0;
    uint32_t Level = 0;
    uint32_t FactionId = 0;
    float LocationX = 0.f;
    float LocationY = 0.f;
    float LocationZ = 0.f;
    uint32_t WorldId = 0;
    uint32_t Path = 0;   // player path (0 Soldier / 1 Settler / 2 Scientist / 3 Explorer) from DB activePath
    std::vector<std::pair<uint32_t, uint32_t>> Appearance;
};

class CharacterListMessage {
public:
    static constexpr uint16_t Opcode = 0x0117;
    static std::vector<uint8_t> Build(const std::vector<CharacterRecord>& characters);
};

} // namespace nexus::proto
