#include "proto/account_realm.h"
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

std::vector<uint8_t> AccountRealmMessages::BuildAccountData() {
    PacketWriter w;
    w.WriteUInt32(0);
    w.WriteUInt32(0);
    w.WriteUInt16(0);
    w.WriteUInt16(0);
    w.WriteUInt64(0);
    w.WriteUInt32(0);
    w.WriteUInt16(0);
    w.WriteBit(false);
    WriteWideString(w, "");
    w.WriteUInt32(0);
    w.WriteBits(0, 2);
    w.WriteBits(0, 21);
    return w.ToArray();
}

std::vector<uint8_t> AccountRealmMessages::Build3db() {
    PacketWriter w;
    w.WriteUInt32(0x7F000001);
    w.WriteUInt16(24000);
    w.WriteUInt32(0); w.WriteUInt16(0); w.WriteUInt16(0); w.WriteUInt64(0);
    w.WriteUInt32(0);
    WriteWideString(w, "");
    w.WriteUInt32(0);
    w.WriteBits(0, 2);
    w.WriteBits(0, 21);
    return w.ToArray();
}

static void WriteRealmEntry(PacketWriter& w, const RealmEntry& r) {
    w.WriteUInt32(r.Id);
    WriteWideString(w, r.Name);
    w.WriteUInt32(r.Field10);
    w.WriteUInt32(r.Field14);
    w.WriteBits(r.PvpType, 2);
    w.WriteBits(r.Status, 3);
    w.WriteBits(r.Population, 3);
    w.WriteUInt32(r.Field24);
    w.WriteUInt64(0); w.WriteUInt64(0);
    w.WriteBits(r.AddrBits14, 14);
    w.WriteUInt32(r.AddrField4);
    WriteWideString(w, r.Host);
    w.WriteUInt64(r.AddrField10);
    w.WriteUInt16(r.Field50);
    w.WriteUInt16(r.Field52);
    w.WriteUInt16(r.Field54);
    w.WriteUInt16(r.Field56);
}

std::vector<uint8_t> AccountRealmMessages::BuildRealmList(const std::vector<RealmEntry>& realms) {
    PacketWriter w;
    w.WriteUInt64(0);
    w.WriteUInt32(static_cast<uint32_t>(realms.size()));
    for (const auto& r : realms) WriteRealmEntry(w, r);
    w.WriteUInt32(0);
    return w.ToArray();
}

} // namespace nexus::proto
