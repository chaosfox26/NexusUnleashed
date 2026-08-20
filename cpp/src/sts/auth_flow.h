#pragma once
#include <cstdint>
#include <optional>
#include <string>
#include <utility>
#include <vector>
#include "sts/sts_server.h"

namespace nexus::sts {

struct IAccountStore {
    virtual ~IAccountStore() = default;
    virtual std::optional<std::pair<std::vector<uint8_t>, std::vector<uint8_t>>>
        GetSrpCredentials(const std::string& login) = 0;
    virtual void StoreGameToken(const std::string& login, const std::string& tokenHex) = 0;
    virtual long GetUserId(const std::string& login) = 0;
};

namespace AuthFlow {
    void Register(StsServer& server, IAccountStore& accounts);
}

namespace AuthSession {
    void Register(const std::string& tokenHex, long accountId);
    long ResolveToken(const std::string& tokenHex);
    long LastAccountId();
}

} // namespace nexus::sts
