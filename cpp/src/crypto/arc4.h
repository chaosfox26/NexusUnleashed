// NexusUnleashed - clean-room authored. Standard RC4/ARC4 stream cipher (public-domain
// algorithm), written from the algorithm — the post-SRP STS channel cipher. State (i,j)
// persists across ProcessBuffer calls, matching the C# reference behavior.
#pragma once
#include <cstdint>
#include <cstddef>
#include <vector>

namespace nexus::crypto {

class Arc4 {
public:
    void PrepareKey(const uint8_t* key, size_t key_len) {
        for (int n = 0; n < 256; ++n) s_[n] = static_cast<uint8_t>(n);
        int j = 0;
        for (int n = 0; n < 256; ++n) {
            j = (j + s_[n] + key[n % key_len]) & 0xFF;
            uint8_t t = s_[n]; s_[n] = s_[j]; s_[j] = t;
        }
        i_ = 0; j_ = 0;
    }
    void PrepareKey(const std::vector<uint8_t>& key) { PrepareKey(key.data(), key.size()); }

    void ProcessBuffer(uint8_t* data, size_t length) {
        for (size_t n = 0; n < length; ++n) {
            i_ = (i_ + 1) & 0xFF;
            j_ = (j_ + s_[i_]) & 0xFF;
            uint8_t t = s_[i_]; s_[i_] = s_[j_]; s_[j_] = t;
            data[n] ^= s_[(s_[i_] + s_[j_]) & 0xFF];
        }
    }
    void ProcessBuffer(std::vector<uint8_t>& data) { ProcessBuffer(data.data(), data.size()); }

private:
    uint8_t s_[256] = {};
    int i_ = 0, j_ = 0;
};

} // namespace nexus::crypto
