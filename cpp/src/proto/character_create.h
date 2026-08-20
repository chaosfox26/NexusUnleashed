// NexusUnleashed - clean-room authored. ServerCharacterCreate result (wire opcode 0xDC,
// dec 220), the reply to the client's 0x5CD5 create request. Derived from the client's
// char-select dispatcher (sub_140020EA0: opcode 220 -> sub_140021FB0) and the result
// handler sub_140021FB0, which reads a u64 character id at struct +0 and a u32 result at
// struct +12 (3 == success). See spec/protocol/character-create-0xDC.md.
#pragma once
#include <cstdint>
#include <string>
#include <vector>

namespace nexus::proto {

// The client's create REQUEST, wire opcode 0x025C (dec 604). After the realm-lane re-key the
// body is byte-aligned: [u32 total][u16 subOp=0x025B][u32 flags][wideString name][u32 appearance...].
// The name is a WildStar wide string: [u8 prefix=(len<<1)|extend][len × u16 LE]. Confirmed live —
// "Perlli Goldsoul" read byte-exact. Race/class/path/sex/faction sit in the trailing u32 block
// (offsets not yet all pinned). See spec/protocol/character-create-0xDC.md.
struct CharacterCreateRequest {
    static constexpr uint16_t Opcode = 0x025C;   // dec 604

    std::string Name;       // UTF-8 (from the wire's UTF-16LE)
    uint32_t CreationId = 0; // CharacterCreation.tbl ID (u32 @ body offset 6) — expands to
                             // race/class/sex/faction/start/items via the client's own table.
    // Parse what we can from a decrypted 0x025C body. Returns false if it doesn't look valid.
    static bool Parse(const std::vector<uint8_t>& body, CharacterCreateRequest& out);
};

struct CharacterCreateResult {
    static constexpr uint16_t Opcode = 0x00DC;   // dec 220

    // Result codes the client's handler recognises (sub_140021FB0):
    //   3 = OK (success -> world entry)   6 = name-conflict-ish (error 143523)
    //   anything else = generic failure (error 143525)
    enum : uint32_t { Ok = 3, NameConflict = 6, GenericFail = 0 };

    // Build the 0xDC body. Layout is the in-memory struct mirror (charId u64 @0, a u32
    // gap @8, result u32 @12) until pinned live against the client's deserializer.
    static std::vector<uint8_t> Build(uint64_t characterId, uint32_t result);
};

} // namespace nexus::proto
