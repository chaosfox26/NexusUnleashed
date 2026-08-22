#include "proto/character_list.h"
#include "net/bitstream.h"

namespace nexus::proto {

using nexus::net::PacketWriter;

static void WriteWideString(PacketWriter& w, const std::string& s) {
    std::vector<uint16_t> units;
    units.reserve(s.size());
    size_t i = 0;
    while (i < s.size()) {
        unsigned char c = static_cast<unsigned char>(s[i]);
        if (c < 0x80) { units.push_back(c); i += 1; }
        else if ((c >> 5) == 0x6 && i + 1 < s.size()) {
            units.push_back(static_cast<uint16_t>(((c & 0x1F) << 6) | (s[i+1] & 0x3F))); i += 2;
        } else if ((c >> 4) == 0xE && i + 2 < s.size()) {
            units.push_back(static_cast<uint16_t>(((c & 0x0F) << 12) | ((s[i+1] & 0x3F) << 6) | (s[i+2] & 0x3F))); i += 3;
        } else { units.push_back(c); i += 1; }
    }

    size_t len = units.size();
    if (len <= 0x7f) { w.WriteBit(false); w.WriteBits(static_cast<uint64_t>(len), 7); }
    else             { w.WriteBit(true);  w.WriteBits(static_cast<uint64_t>(len), 15); }
    for (uint16_t u : units) w.WriteUInt16(u);
}

static void WriteCharacter(PacketWriter& w, const CharacterRecord& c) {
    w.WriteUInt64(c.Id);
    WriteWideString(w, c.Name);
    w.WriteBits(c.Sex, 2);
    w.WriteBits(c.Race, 5);
    w.WriteBits(c.Class, 5);
    w.WriteUInt32(c.WorldId);
    w.WriteUInt32(c.Level);
    w.WriteUInt32(static_cast<uint32_t>(c.Appearance.size()));
    for (const auto& v : c.Appearance) {
        w.WriteBits(v.first, 7);
        w.WriteBits(v.second, 15);
        w.WriteBits(0, 14);
        w.WriteUInt32(0);
    }
    w.WriteUInt32(0);
    w.WriteBits(0, 15);
    w.WriteBits(0, 15);
    w.WriteBits(0, 14);
    w.WriteSingle(c.LocationX);
    w.WriteSingle(c.LocationY);
    w.WriteSingle(c.LocationZ);
    w.WriteSingle(0.f);
    w.WriteSingle(0.f);
    w.WriteBits(c.Path & 0x7, 3);   // player path -> char-select carries it into the world so
    w.WriteBit(false);              // PathTracker's GetPlayerPathType() is valid at addon load
    w.WriteBit(false);
    w.WriteUInt32(c.FactionId);
    w.WriteBits(0, 4);
    w.WriteUInt32(0);
    w.WriteSingle(0.f);
}

std::vector<uint8_t> CharacterListMessage::Build(const std::vector<CharacterRecord>& characters) {
    PacketWriter w;
    w.WriteUInt64(0);
    w.WriteUInt32(static_cast<uint32_t>(characters.size()));
    for (const auto& c : characters) WriteCharacter(w, c);
    w.WriteUInt32(0);
    w.WriteUInt32(0);
    w.WriteBits(0, 14);
    w.WriteBits(0, 14); w.WriteUInt64(0);
    w.WriteUInt32(0);
    w.WriteUInt32(0);
    w.WriteUInt32(0);
    w.WriteUInt32(0);
    w.WriteBits(0, 14);
    w.WriteBit(false);
    return w.ToArray();
}

} // namespace nexus::proto
