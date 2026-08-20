// NexusUnleashed - clean-room authored. See character_create.h.
#include "proto/character_create.h"
#include "net/bitstream.h"

namespace nexus::proto {

using nexus::net::PacketWriter;
using nexus::net::PacketReader;

bool CharacterCreateRequest::Parse(const std::vector<uint8_t>& body, CharacterCreateRequest& out) {
    // The body is byte-aligned. Header: u32 total, u16 subOp, u32 flags, then the name.
    if (body.size() < 11) return false;
    try {
        PacketReader r(body);
        (void)r.ReadUInt32();                 // total length (echoes body size)
        uint16_t subOp = r.ReadUInt16();      // expected 0x025B
        out.CreationId = r.ReadUInt32();      // CharacterCreation.tbl ID (was mislabeled "flags")
        uint8_t prefix = r.ReadByte();        // wide-string prefix: (len<<1)|extend
        uint32_t len = (prefix & 1) ? 0 : (uint32_t)(prefix >> 1);
        if (len == 0 || len > 32) return false;   // WildStar names are <= ~29 chars
        std::string name;
        for (uint32_t i = 0; i < len; ++i) {
            uint16_t ch = r.ReadUInt16();     // UTF-16LE code unit
            if (ch < 0x80) name += (char)ch;  // ASCII fast path (names are latin)
            else if (ch < 0x800) { name += (char)(0xC0 | (ch >> 6)); name += (char)(0x80 | (ch & 0x3F)); }
            else { name += (char)(0xE0 | (ch >> 12)); name += (char)(0x80 | ((ch >> 6) & 0x3F)); name += (char)(0x80 | (ch & 0x3F)); }
        }
        (void)subOp;
        out.Name = name;
        return !name.empty();
    } catch (const std::exception&) {
        return false;
    }
}

std::vector<uint8_t> CharacterCreateResult::Build(uint64_t characterId, uint32_t result) {
    PacketWriter w;
    w.WriteUInt64(characterId);   // +0x00 new character id
    w.WriteUInt32(0);             // +0x08 (unread by the client; kept for struct alignment)
    w.WriteUInt32(result);        // +0x0C result code (3 = success)
    return w.ToArray();
}

} // namespace nexus::proto
