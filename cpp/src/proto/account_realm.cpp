// NexusUnleashed - clean-room authored. See account_realm.h.
// Layout reversed from the client Read functions (WS+0xA2110 / WS+0xAC9D0) and verified by
// deser.py. spec/protocol/realm-list-0x761-and-account-0x7A1.md.
#include "proto/account_realm.h"
#include "net/bitstream.h"

namespace nexus::proto {

using nexus::net::PacketWriter;

// Wide-string wire form (WS+0x336A40): [1b lenType][7|15b len][len × u16]. UTF-8 -> UTF-16
// code units (BMP fast path; ASCII is 1:1 — enough for realm/host names). Same as 0x0117.
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

// 0x7A1 body: u32 · {u32,u16,u16,8B} · {u32,u16} · 1b · wstr · u32 · 2b · 21b
std::vector<uint8_t> AccountRealmMessages::BuildAccountData() {
    PacketWriter w;
    w.WriteUInt32(0);                 // +0x00 f00
    // composite @+0x04 (16 bytes): the 4 dwords copied to global+0x1638, reused on realm-enter
    w.WriteUInt32(0);                 //   +0x04 u32
    w.WriteUInt16(0);                 //   +0x08 u16
    w.WriteUInt16(0);                 //   +0x0a u16
    w.WriteUInt64(0);                 //   +0x0c 8 raw bytes (64 bits)
    // composite @+0x14 (8 bytes)
    w.WriteUInt32(0);                 //   +0x14 u32
    w.WriteUInt16(0);                 //   +0x18 u16
    w.WriteBit(false);                // +0x1c flag (1 bit)
    WriteWideString(w, "");           // +0x20 wstring
    w.WriteUInt32(0);                 // +0x28 f28
    w.WriteBits(0, 2);                // +0x2c (2 bits)
    w.WriteBits(0, 21);               // +0x30 (21 bits)
    return w.ToArray();
}

// 0x3db body (Read WS+0x7D6A0): u32 · u16 · {u32,u16,u16,8 bytes} · u32 · wstring · u32 · 2b · 21b
std::vector<uint8_t> AccountRealmMessages::Build3db() {
    PacketWriter w;
    // +0x00/+0x04 are the REALM address the client dials next (sub_140334BB0: htonl(ip), htons(port)).
    // Zeros here made the client connect() to 0.0.0.0:0 and hang at "Connecting to realm".
    // 127.0.0.1 host-order = 0x7F000001; realm connection served on 24000.
    w.WriteUInt32(0x7F000001);        // +0x00 realm IP = 127.0.0.1
    w.WriteUInt16(24000);             // +0x04 realm port
    w.WriteUInt32(0); w.WriteUInt16(0); w.WriteUInt16(0); w.WriteUInt64(0);  // +0x08 composite (16 bytes)
    w.WriteUInt32(0);                 // +0x18
    WriteWideString(w, "");           // +0x20 wstring
    w.WriteUInt32(0);                 // +0x28
    w.WriteBits(0, 2);                // +0x2c
    w.WriteBits(0, 21);               // +0x30
    return w.ToArray();
}

// One realm entry (WS+0xac130), 0x58-byte client struct.
static void WriteRealmEntry(PacketWriter& w, const RealmEntry& r) {
    w.WriteUInt32(r.Id);              // +0x00 nRealmId
    WriteWideString(w, r.Name);       // +0x08 strName
    w.WriteUInt32(r.Field10);         // +0x10
    w.WriteUInt32(r.Field14);         // +0x14
    w.WriteBits(r.PvpType, 2);        // +0x18 nRealmPVPType
    w.WriteBits(r.Status, 3);         // +0x1c nRealmStatus
    w.WriteBits(r.Population, 3);     // +0x20 nPopulation
    w.WriteUInt32(r.Field24);         // +0x24
    w.WriteUInt64(0); w.WriteUInt64(0); // +0x28 16 raw bytes (128 bits)
    // address composite @+0x38 (WS+0xabc00)
    w.WriteBits(r.AddrBits14, 14);    //   14 bits
    w.WriteUInt32(r.AddrField4);      //   u32
    WriteWideString(w, r.Host);       //   wstring host
    w.WriteUInt64(r.AddrField10);     //   u64
    w.WriteUInt16(r.Field50);         // +0x50
    w.WriteUInt16(r.Field52);         // +0x52
    w.WriteUInt16(r.Field54);         // +0x54
    w.WriteUInt16(r.Field56);         // +0x56
}

// 0x761 body: u64 · u32 count1 · count1×RealmEntry · u32 count2 · count2×Entry2
std::vector<uint8_t> AccountRealmMessages::BuildRealmList(const std::vector<RealmEntry>& realms) {
    PacketWriter w;
    w.WriteUInt64(0);                                       // +0x00 header
    w.WriteUInt32(static_cast<uint32_t>(realms.size()));    // +0x08 count1
    for (const auto& r : realms) WriteRealmEntry(w, r);
    w.WriteUInt32(0);                                       // +0x18 count2 (Entry2 array) — empty
    return w.ToArray();
}

} // namespace nexus::proto
