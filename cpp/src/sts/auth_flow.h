// NexusUnleashed - clean-room authored. C++ port of AuthFlow.cs + AuthSession.cs. The STS
// login transaction (Connect -> LoginStart -> KeyData -> LoginFinish -> ListMyAccounts ->
// RequestGameToken) over the ported SRP, plus the in-process bridge to the realm channel.
#pragma once
#include <cstdint>
#include <optional>
#include <string>
#include <utility>
#include <vector>
#include "sts/sts_server.h"

namespace nexus::sts {

/// Account backend (DB or in-memory). Synchronous — the login server is low-concurrency.
struct IAccountStore {
    virtual ~IAccountStore() = default;
    /// (salt, verifier) for the account, or nullopt if unknown.
    virtual std::optional<std::pair<std::vector<uint8_t>, std::vector<uint8_t>>>
        GetSrpCredentials(const std::string& login) = 0;
    virtual void StoreGameToken(const std::string& login, const std::string& tokenHex) = 0;
    virtual long GetUserId(const std::string& login) = 0;
};

namespace AuthFlow {
    void Register(StsServer& server, IAccountStore& accounts);
}

/// Shared bridge (AuthSession.cs): the authenticated account -> the realm channel.
namespace AuthSession {
    void Register(const std::string& tokenHex, long accountId);
    long ResolveToken(const std::string& tokenHex);
    long LastAccountId();
}

} // namespace nexus::sts
