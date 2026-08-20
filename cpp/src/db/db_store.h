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

// Fields the client sends in a 0x5CD5 create request (name + race/class/path/sex/faction
// + a start location the server assigns). Appearance/bones are carried separately.
struct NewCharacter {
    std::string Name;
    uint32_t Sex = 0, Race = 0, Class = 0, FactionId = 0, ActivePath = 0;
    uint32_t WorldId = 0, WorldZoneId = 0;
    float LocationX = 0.f, LocationY = 0.f, LocationZ = 0.f;
};

class DbCharacterStore {
public:
    explicit DbCharacterStore(const std::string& authConnString);  // swaps db -> characterdb
    std::vector<proto::CharacterRecord> GetCharacters(long accountId);
    // Insert a new character for the account; returns its assigned id (0 on failure).
    // Id is generated as MAX(id)+1 because the character.id column is a manual PK.
    uint64_t CreateCharacter(long accountId, const NewCharacter& nc);
private:
    ConnInfo ci_;
};

} // namespace nexus::db
