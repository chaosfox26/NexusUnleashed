#pragma once
#include <optional>
#include <string>
#include <utility>
#include <vector>
#include "sts/auth_flow.h"
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

struct NewCharacter {
    std::string Name;
    uint32_t Sex = 0, Race = 0, Class = 0, FactionId = 0, ActivePath = 0;
    uint32_t WorldId = 0, WorldZoneId = 0;
    float LocationX = 0.f, LocationY = 0.f, LocationZ = 0.f;
    std::vector<std::pair<uint32_t, uint32_t>> Customization;
};

class DbCharacterStore {
public:
    explicit DbCharacterStore(const std::string& authConnString);
    std::vector<proto::CharacterRecord> GetCharacters(long accountId);
    uint64_t CreateCharacter(long accountId, const NewCharacter& nc);
    bool DeleteCharacter(long accountId, uint64_t characterId);
private:
    ConnInfo ci_;
};

} // namespace nexus::db
