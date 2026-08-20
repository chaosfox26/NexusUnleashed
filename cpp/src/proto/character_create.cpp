#include "proto/character_create.h"
#include "net/bitstream.h"

namespace nexus::proto {

using nexus::net::PacketWriter;
using nexus::net::PacketReader;

bool CharacterCreateRequest::Parse(const std::vector<uint8_t>& body, CharacterCreateRequest& out) {
    if (body.size() < 11) return false;
    try {
        PacketReader r(body);
        (void)r.ReadUInt32();
        uint16_t subOp = r.ReadUInt16();
        out.CreationId = r.ReadUInt32();
        uint8_t prefix = r.ReadByte();
        uint32_t len = (prefix & 1) ? 0 : (uint32_t)(prefix >> 1);
        if (len == 0 || len > 32) return false;
        std::string name;
        for (uint32_t i = 0; i < len; ++i) {
            uint16_t ch = r.ReadUInt16();
            if (ch < 0x80) name += (char)ch;
            else if (ch < 0x800) { name += (char)(0xC0 | (ch >> 6)); name += (char)(0x80 | (ch & 0x3F)); }
            else { name += (char)(0xE0 | (ch >> 12)); name += (char)(0x80 | ((ch >> 6) & 0x3F)); name += (char)(0x80 | (ch & 0x3F)); }
        }
        (void)subOp;
        out.Name = name;
        if (name.empty()) return false;

        try {
            uint32_t count = r.ReadUInt32() >> 3;
            if (count > 0 && count <= 64) {
                std::vector<uint32_t> labels(count), values(count);
                for (uint32_t i = 0; i < count; ++i) labels[i] = r.ReadUInt32() >> 3;
                for (uint32_t i = 0; i < count; ++i) values[i] = r.ReadUInt32() >> 3;
                out.Customization.reserve(count);
                for (uint32_t i = 0; i < count; ++i)
                    out.Customization.emplace_back(labels[i], values[i]);
            }
        } catch (const std::exception&) {
            out.Customization.clear();
        }
        return true;
    } catch (const std::exception&) {
        return false;
    }
}

std::vector<uint8_t> CharacterCreateResult::Build(uint64_t characterId, uint32_t result) {
    PacketWriter w;
    w.WriteUInt64(characterId);
    w.WriteUInt32(0);
    w.WriteUInt32(result);
    return w.ToArray();
}

} // namespace nexus::proto
