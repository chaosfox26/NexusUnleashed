#pragma once
#include <cstdint>
#include <string>
#include <vector>
#include <utility>

namespace nexus::proto {

struct CharacterCreateRequest {
    static constexpr uint16_t Opcode = 0x025C;

    std::string Name;
    uint32_t CreationId = 0;
    std::vector<std::pair<uint32_t, uint32_t>> Customization;
    static bool Parse(const std::vector<uint8_t>& body, CharacterCreateRequest& out);
};

struct CharacterCreateResult {
    static constexpr uint16_t Opcode = 0x00DC;

    enum : uint32_t { Ok = 3, NameConflict = 6, GenericFail = 0 };

    static std::vector<uint8_t> Build(uint64_t characterId, uint32_t result);
};

} // namespace nexus::proto
