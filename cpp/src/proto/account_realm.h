// NexusUnleashed - clean-room authored. The two account-retrieval pushes the client's
// account state (WS+0x45A70) waits for after realm-enter 0x0592:
//   0x7A1 ServerAccountData  (Read WS+0xA2110)  -> state 1->2
//   0x761 ServerRealmList    (Read WS+0xAC9D0)  -> fires RealmListChanged + clears the
//                                                   "Retrieving Account Information" overlay
// Wire layout reversed from the client's own deserializers and machine-verified (deser.py).
// Full map + field semantics: spec/protocol/realm-list-0x761-and-account-0x7A1.md.
// Bits LSB-first (PacketWriter). Both are sent CLEAR-framed on the realm channel like 0x0117.
#pragma once
#include <cstdint>
#include <string>
#include <vector>

namespace nexus::proto {

/// One realm as the client's realm-list deserializer (WS+0xac130) expects it.
struct RealmEntry {
    uint32_t Id = 1;              // +0x00  nRealmId
    std::string Name;             // +0x08  strName
    uint32_t Field10 = 0;         // +0x10  (candidate char-count; unconfirmed)
    uint32_t Field14 = 0;         // +0x14
    uint32_t PvpType = 0;         // +0x18  nRealmPVPType  (2 bits: 0 PvE / 1 PvP)
    uint32_t Status = 0;          // +0x1c  nRealmStatus   (3 bits: 0 Up ...)
    uint32_t Population = 0;      // +0x20  nPopulation    (3 bits: 0 Low ...)
    uint32_t Field24 = 0;         // +0x24
    // address composite @+0x38 (WS+0xabc00) = { 14 bits, u32, wstring host, u64 }
    uint32_t AddrBits14 = 0;      //   14 bits
    uint32_t AddrField4 = 0;      //   u32
    std::string Host;             //   wstring host (reconnect target — NEEDS LIVE VERIFY)
    uint64_t AddrField10 = 0;     //   u64 (port? — NEEDS LIVE VERIFY)
    uint16_t Field50 = 0;
    uint16_t Field52 = 0;
    uint16_t Field54 = 0;
    uint16_t Field56 = 0;
};

class AccountRealmMessages {
public:
    static constexpr uint16_t OpAccountData = 0x07A1;
    static constexpr uint16_t OpRealmList   = 0x0761;

    // 0x7A1 — minimal account-data payload (zeros advance state 1->2).
    static std::vector<uint8_t> BuildAccountData();

    // 0x3db — connection handshake step 2. Read WS+0x7D6A0: u32, u16, {u32,u16,u16,8B}, u32,
    // wstring, u32, 2b, 21b. Handled at conn state 9 (sub_140037F30): installs the next cipher
    // and advances to state 10. Minimal (zero) payload.
    static std::vector<uint8_t> Build3db();

    // 0x761 — realm list. Empty (realms.empty()) is the safest first test: it still fires
    // RealmListChanged and clears the overlay. One entry lets the player pick our realm.
    static std::vector<uint8_t> BuildRealmList(const std::vector<RealmEntry>& realms);
};

} // namespace nexus::proto
