// NexusUnleashed - clean-room authored. C++ port of CharacterListMessage.cs. The 0x0117
// char-list body — wire layout read from the client's own deserializer (WS+0x7FAB0 /
// WS+0x7F720) and VALIDATED live (Read returns eax=0). Full map:
// spec/protocol/char-list-0x117.md. Bits LSB-first (PacketWriter).
#pragma once
#include <cstdint>
#include <string>
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
};

class CharacterListMessage {
public:
    static constexpr uint16_t Opcode = 0x0117;
    static std::vector<uint8_t> Build(const std::vector<CharacterRecord>& characters);
};

} // namespace nexus::proto
