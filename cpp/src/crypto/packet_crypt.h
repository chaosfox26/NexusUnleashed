// NexusUnleashed - clean-room authored. WildStar's realm/world packet cipher, reversed 1:1
// from the client and VALIDATED offline against a captured plaintext/ciphertext pair (the
// 0x0592 container decrypts byte-exact). Both directions use this ONE cipher.
// Client routines: key expansion WS+0xC2EB0, ctor WS+0xC2BD0 (also folds the register),
// encrypt WS+0xC2D10 (feedback=output), decrypt WS+0xC2DE0 (feedback=input). Cross-checked
// against the Hex-Rays decompilation (_Client-RE/.../sub_1400C2D10.c).
//
// - key table: 16 qwords. key[0]=(kSeedInitial+seed)*MULT; key[i]=(key[i-1]+seed)*MULT.
// - register (initial feedback): reg=kSeedInitial; for each key qword: reg=(key[i]+reg)*MULT.
// - process: qword CFB. counter=(uint32)(len*(MULT+1)); idx=counter&0xF, counter++ per block;
//   out_q = key[idx] ^ in_q ^ reg; reg = feedbackOutput ? out_q : in_q. A byte-wise tail XORs
//   the remaining <8 bytes with key[idx] bytes and the register bytes (no feedback update).
// - STATELESS per message (register resets to the folded value each message).
#pragma once
#include <cstdint>
#include <vector>

namespace nexus::crypto {

class PacketCrypt {
public:
    static constexpr uint64_t AuthChannelKey    = 0xD283F5B34A8DC685ull;
    static constexpr uint64_t WorldChannelSeed  = AuthChannelKey;

    explicit PacketCrypt(uint64_t seed);

    std::vector<uint8_t> Encrypt(const uint8_t* data, size_t length) const;          // feedback=output
    std::vector<uint8_t> EncryptForClient(const uint8_t* data, size_t length) const { return Encrypt(data, length); }
    std::vector<uint8_t> Decrypt(const uint8_t* data, size_t length) const;          // feedback=input

    std::vector<uint8_t> Encrypt(const std::vector<uint8_t>& d) const { return Encrypt(d.data(), d.size()); }
    std::vector<uint8_t> EncryptForClient(const std::vector<uint8_t>& d) const { return Encrypt(d.data(), d.size()); }
    std::vector<uint8_t> Decrypt(const std::vector<uint8_t>& d) const { return Decrypt(d.data(), d.size()); }

private:
    static constexpr uint64_t kSeedInitial = 0x718DA9074F2DEB91ull;
    static constexpr uint64_t kMultiplier  = 0xAA7F8EA9ull;
    static constexpr uint32_t kCounterMult = 0xAA7F8EAAu;   // == kMultiplier + 1

    std::vector<uint8_t> Process(const uint8_t* data, size_t length, bool feedbackOutput) const;

    uint64_t keyq_[16];
    uint64_t register_;   // folded initial feedback
};

} // namespace nexus::crypto
