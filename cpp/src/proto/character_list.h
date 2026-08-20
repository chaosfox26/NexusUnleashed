// NexusUnleashed - clean-room authored. C++ port of CharacterListMessage.cs. The 0x0117
// char-list body — wire layout read from the client's own deserializer (WS+0x7FAB0 /
// WS+0x7F720) and VALIDATED live (Read returns eax=0). Full map:
// spec/protocol/char-list-0x117.md. Bits LSB-first (PacketWriter).
#pragma once
#include <cstdint>
#include <string>
#include <utility>
#include <vector>

namespace nexus::proto {

/// One character row as the client's char-list deserializer expects it.
struct CharacterRecord {
    uint64_t Id = 0;
    std::string Name;          // UTF-8 (widened to UTF-16 code units on the wire)
    uint32_t Sex = 0;          // 2 bits
    uint32_t Race = 0;         // 5 bits
    uint32_t Class = 0;        // 5 bits
    uint32_t Level = 0;        // +0x1c u32 (INFERRED slot)
    uint32_t FactionId = 0;    // +0x20 u32 (INFERRED slot)
    float LocationX = 0.f;
    float LocationY = 0.f;
    float LocationZ = 0.f;
    uint32_t WorldId = 0;
    // Equipped/customization visuals (character_appearance rows: slot -> displayId). These are
    // the FIRST appearance list in the record; each item is {slot:7b, displayId:15b, 0:14b, 0:32b}
    // and the client's record reader (WS+0x7F720) drops them into the model's visual array. Empty
    // => black silhouette (no skin/hair/eye/gear textures). Derived from the client's own
    // CharacterCustomization table; see GameData::ResolveAppearance.
    std::vector<std::pair<uint32_t, uint32_t>> Appearance;   // (itemSlot, itemDisplayId)
};

class CharacterListMessage {
public:
    static constexpr uint16_t Opcode = 0x0117;
    static std::vector<uint8_t> Build(const std::vector<CharacterRecord>& characters);
};

} // namespace nexus::proto
