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

    std::vector<uint8_t> StartHandshake();

    bool Verify(const std::vector<uint8_t>& aLe, const std::vector<uint8_t>& m1,
                std::vector<uint8_t>& m2, std::vector<uint8_t>& sessionKey);

    const std::vector<uint8_t>& B() const { return b_wire_; }

private:
    struct Impl;
    std::unique_ptr<Impl> p_;
    std::vector<uint8_t> b_wire_;
};

} // namespace nexus::crypto
