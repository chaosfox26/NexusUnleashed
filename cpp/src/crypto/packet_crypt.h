#pragma once
#include <cstdint>
#include <vector>

namespace nexus::crypto {

class PacketCrypt {
public:
    static constexpr uint64_t AuthChannelKey    = 0xD283F5B34A8DC685ull;
    static constexpr uint64_t WorldChannelSeed  = AuthChannelKey;
    static constexpr uint64_t RealmLaneKey      = 0x9A868DE642EF9906ull;

    explicit PacketCrypt(uint64_t seed);

    std::vector<uint8_t> Encrypt(const uint8_t* data, size_t length) const;
    std::vector<uint8_t> EncryptForClient(const uint8_t* data, size_t length) const { return Encrypt(data, length); }
    std::vector<uint8_t> Decrypt(const uint8_t* data, size_t length) const;

    std::vector<uint8_t> Encrypt(const std::vector<uint8_t>& d) const { return Encrypt(d.data(), d.size()); }
    std::vector<uint8_t> EncryptForClient(const std::vector<uint8_t>& d) const { return Encrypt(d.data(), d.size()); }
    std::vector<uint8_t> Decrypt(const std::vector<uint8_t>& d) const { return Decrypt(d.data(), d.size()); }

private:
    static constexpr uint64_t kSeedInitial = 0x718DA9074F2DEB91ull;
    static constexpr uint64_t kMultiplier  = 0xAA7F8EA9ull;
    static constexpr uint32_t kCounterMult = 0xAA7F8EAAu;

    std::vector<uint8_t> Process(const uint8_t* data, size_t length, bool feedbackOutput) const;

    uint64_t keyq_[16];
    uint64_t register_;
};

} // namespace nexus::crypto
