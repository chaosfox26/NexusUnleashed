// NexusUnleashed - clean-room authored. See packet_crypt.h. Reversed 1:1 from the client
// (WS+0xC2EB0/0xC2BD0/0xC2D10/0xC2DE0) and validated byte-exact against a captured pair.
#include "crypto/packet_crypt.h"

namespace nexus::crypto {

static uint64_t ReadU64LE(const uint8_t* p) {
    uint64_t v = 0; for (int i = 0; i < 8; ++i) v |= static_cast<uint64_t>(p[i]) << (8 * i); return v;
}
static void WriteU64LE(uint8_t* p, uint64_t v) {
    for (int i = 0; i < 8; ++i) p[i] = static_cast<uint8_t>(v >> (8 * i));
}

PacketCrypt::PacketCrypt(uint64_t seed) {
    uint64_t q = (kSeedInitial + seed) * kMultiplier;
    for (int i = 0; i < 16; ++i) { keyq_[i] = q; q = (q + seed) * kMultiplier; }
    // Fold the key table into the initial feedback register (client ctor WS+0xC2BD0).
    uint64_t reg = kSeedInitial;
    for (int i = 0; i < 16; ++i) reg = (keyq_[i] + reg) * kMultiplier;
    register_ = reg;
}

std::vector<uint8_t> PacketCrypt::Process(const uint8_t* data, size_t length, bool feedbackOutput) const {
    std::vector<uint8_t> out(data, data + length);
    uint64_t reg = register_;
    uint32_t counter = static_cast<uint32_t>(length) * kCounterMult;
    size_t full = length / 8;
    for (size_t b = 0; b < full; ++b) {
        uint32_t idx = counter & 0xF; ++counter;
        uint64_t in_q = ReadU64LE(out.data() + b * 8);
        uint64_t out_q = keyq_[idx] ^ in_q ^ reg;
        WriteU64LE(out.data() + b * 8, out_q);
        reg = feedbackOutput ? out_q : in_q;
    }
    size_t rem = length & 7;
    if (rem) {
        uint32_t idx = counter & 0xF;
        uint8_t kb[8]; WriteU64LE(kb, keyq_[idx]);
        uint8_t rb[8]; WriteU64LE(rb, reg);
        for (size_t i = 0; i < rem; ++i)
            out[full * 8 + i] = static_cast<uint8_t>(out[full * 8 + i] ^ kb[i & 7] ^ rb[i]);
    }
    return out;
}

std::vector<uint8_t> PacketCrypt::Encrypt(const uint8_t* data, size_t length) const {
    return Process(data, length, /*feedbackOutput=*/true);
}
std::vector<uint8_t> PacketCrypt::Decrypt(const uint8_t* data, size_t length) const {
    return Process(data, length, /*feedbackOutput=*/false);
}

} // namespace nexus::crypto
