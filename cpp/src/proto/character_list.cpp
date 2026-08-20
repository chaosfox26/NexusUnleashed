// NexusUnleashed - clean-room authored. See character_list.h. 1:1 with CharacterListMessage.cs.
#include "proto/character_list.h"
#include "net/bitstream.h"

namespace nexus::proto {

using nexus::net::PacketWriter;

// Wide-string wire form (WS+0x336A40): [1b lenType][lenType==0 ? 7b len : 15b len][len × u16].
// Name is UTF-8; widen per-code-unit. ASCII (WildStar names) is 1:1; a full UTF-8 decode
// is a later refinement (TODO) — flagged so it isn't mistaken for complete.
static void WriteWideString(PacketWriter& w, const std::string& s) {
    // Minimal UTF-8 -> UTF-16 code units (BMP). ASCII fast-path covers the common case.
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
        } else { units.push_back(c); i += 1; } // lone byte / unsupported: pass through
    }

    size_t len = units.size();
    if (len <= 0x7f) { w.WriteBit(false); w.WriteBits(static_cast<uint64_t>(len), 7); }
    else             { w.WriteBit(true);  w.WriteBits(static_cast<uint64_t>(len), 15); }
    for (uint16_t u : units) w.WriteUInt16(u);
}

// Per-character record (WS+0x7F720), 0xA0 bytes in the client's struct.
static void WriteCharacter(PacketWriter& w, const CharacterRecord& c) {
    w.WriteUInt64(c.Id);                    // +0x00 character id
    WriteWideString(w, c.Name);             // +0x08 name (WS+0x336A40)
    w.WriteBits(c.Sex, 2);                  // +0x10
    w.WriteBits(c.Race, 5);                 // +0x14
    w.WriteBits(c.Class, 5);                // +0x18
    w.WriteUInt32(c.WorldId);               // +0x1c idWorld  (PINNED live)
    w.WriteUInt32(c.Level);                 // +0x20 nLevel   (PINNED: a char showed its faction
                                            //   id as its level when FactionId sat here)
    // +0x24 countA: the character's visuals. Reader WS+0x7F720 loops countA items, each read by
    // WS+0xAB890 as {7b, 15b, 14b, 32b}; WS+0x201F0 stores item[1] into the model's slot array at
    // index item[0]. So item = {slot, displayId, 0, 0}. This is what makes the model render.
    w.WriteUInt32(static_cast<uint32_t>(c.Appearance.size()));
    for (const auto& v : c.Appearance) {
        w.WriteBits(v.first, 7);            //   slot      (index into the visual array, < 72)
        w.WriteBits(v.second, 15);          //   displayId (item display id, <= 32767)
        w.WriteBits(0, 14);                 //   dye lookup key (0 = no dye)
        w.WriteUInt32(0);                   //   packed dye color (0 = none)
    }
    w.WriteUInt32(0);                       // +0x30 countB (appearance list 2) — empty
    w.WriteBits(0, 15);                     // +0x40
    w.WriteBits(0, 15);                     // +0x44
    w.WriteBits(0, 14);                     // +0x48
    w.WriteSingle(c.LocationX);             // +0x4c vec5 (WS+0xAB810 reads FIVE floats)
    w.WriteSingle(c.LocationY);
    w.WriteSingle(c.LocationZ);
    w.WriteSingle(0.f);
    w.WriteSingle(0.f);
    w.WriteBits(0, 3);                      // +0x60
    w.WriteBit(false);                      // +0x64
    w.WriteBit(false);                      // +0x68
    w.WriteUInt32(c.FactionId);             // +0x6c idFaction (read last in the tChar builder;
                                            //   stored last of the three in WS+0x201F0)
    w.WriteBits(0, 4);                      // +0x70 countC — empty
    w.WriteUInt32(0);                       // +0x88 countD — empty
    w.WriteSingle(0.f);                     // +0x98 float (WS+0x6C1C0)
}

std::vector<uint8_t> CharacterListMessage::Build(const std::vector<CharacterRecord>& characters) {
    PacketWriter w;
    w.WriteUInt64(0);                                    // +0x00 header (INFERRED)
    w.WriteUInt32(static_cast<uint32_t>(characters.size())); // +0x08 count
    for (const auto& c : characters) WriteCharacter(w, c);
    w.WriteUInt32(0);                                    // +0x18 count2
    w.WriteUInt32(0);                                    // +0x28 count3
    w.WriteBits(0, 14);                                  // +0x38
    w.WriteBits(0, 14); w.WriteUInt64(0);                // +0x40 {14b, u64}
    w.WriteUInt32(0);                                    // +0x50
    w.WriteUInt32(0);                                    // +0x54
    w.WriteUInt32(0);                                    // +0x58
    w.WriteUInt32(0);                                    // +0x5c
    w.WriteBits(0, 14);                                  // +0x60
    w.WriteBit(false);                                   // +0x64 (1 bit)
    return w.ToArray();
}

} // namespace nexus::proto
