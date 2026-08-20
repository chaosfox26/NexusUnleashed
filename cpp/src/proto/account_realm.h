#pragma once
#include <cstdint>
#include <string>
#include <vector>

namespace nexus::proto {

struct RealmEntry {
    uint32_t Id = 1;
    std::string Name;
    uint32_t Field10 = 0;
    uint32_t Field14 = 0;
    uint32_t PvpType = 0;
    uint32_t Status = 0;
    uint32_t Population = 0;
    uint32_t Field24 = 0;
    uint32_t AddrBits14 = 0;
    uint32_t AddrField4 = 0;
    std::string Host;
    uint64_t AddrField10 = 0;
    uint16_t Field50 = 0;
    uint16_t Field52 = 0;
    uint16_t Field54 = 0;
    uint16_t Field56 = 0;
};

class AccountRealmMessages {
public:
    static constexpr uint16_t OpAccountData = 0x07A1;
    static constexpr uint16_t OpRealmList   = 0x0761;

    static std::vector<uint8_t> BuildAccountData();
    static std::vector<uint8_t> Build3db();
    static std::vector<uint8_t> BuildRealmList(const std::vector<RealmEntry>& realms);
};

} // namespace nexus::proto
