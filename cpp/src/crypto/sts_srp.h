// NexusUnleashed - clean-room authored. C++ port of StsSrp.cs — WildStar GAME SRP for
// the STS login channel (LITTLE-ENDIAN: modulus read LE, ReverseUInt32 word-order
// hashing, LE bignums, interleaved session key; SHA-256 throughout). Mirrors the client
// parameter-for-parameter so the client's own proof verifies. Bignum + hash = OpenSSL.
#pragma once
#include <cstdint>
#include <memory>
#include <string>
#include <vector>

namespace nexus::crypto {

class StsSrp {
public:
    StsSrp(const std::vector<uint8_t>& salt, const std::vector<uint8_t>& verifier,
           const std::string& username = "");
    ~StsSrp();
    StsSrp(const StsSrp&) = delete;
    StsSrp& operator=(const StsSrp&) = delete;

    /// b random, B = (k*v + g^b) mod N; returns B as 128-byte little-endian.
    std::vector<uint8_t> StartHandshake();

    /// Verify the client proof. A and M1 are little-endian wire bytes.
    /// On success sets m2 (= H(A|M1|K)) and sessionKey (K).
    bool Verify(const std::vector<uint8_t>& aLe, const std::vector<uint8_t>& m1,
                std::vector<uint8_t>& m2, std::vector<uint8_t>& sessionKey);

    const std::vector<uint8_t>& B() const { return b_wire_; }

private:
    struct Impl;
    std::unique_ptr<Impl> p_;
    std::vector<uint8_t> b_wire_;   // B as 128-byte LE
};

} // namespace nexus::crypto
