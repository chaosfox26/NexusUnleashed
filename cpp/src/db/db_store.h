// NexusUnleashed - clean-room authored. C++ port of DbAccountStore.cs / DbCharacterStore.cs.
// authdb.account (SRP salt/verifier as hex, gameToken) + characterdb.character, via
// libmariadb (MariaDB :3307). MySqlConnector is MIT; libmariadb is LGPL (linked, not
// modified). Connection string is the C# form: Server=..;Port=..;User=..;Password=..;Database=..
#pragma once
#include <optional>
#include <string>
#include <utility>
#include <vector>
#include "sts/auth_flow.h"          // IAccountStore
#include "proto/character_list.h"

namespace nexus::db {

struct ConnInfo {
    std::string host = "127.0.0.1";
    unsigned port = 3306;
    std::string user, pass, database;
};
ConnInfo ParseConnString(const std::string& s);

class DbAccountStore : public sts::IAccountStore {
public:
    explicit DbAccountStore(const std::string& connString);
    std::optional<std::pair<std::vector<uint8_t>, std::vector<uint8_t>>>
        GetSrpCredentials(const std::string& login) override;
    void StoreGameToken(const std::string& login, const std::string& tokenHex) override;
    long GetUserId(const std::string& login) override;
private:
    ConnInfo ci_;
};

class DbCharacterStore {
public:
    explicit DbCharacterStore(const std::string& authConnString);  // swaps db -> characterdb
    std::vector<proto::CharacterRecord> GetCharacters(long accountId);
private:
    ConnInfo ci_;
};

} // namespace nexus::db
