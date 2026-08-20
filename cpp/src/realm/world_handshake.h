#pragma once
#include <cstdint>
#include <functional>
#include <utility>
#include <vector>
#include "net/game_server.h"

namespace nexus::realm {

struct WorldHandshake {
    static std::function<std::vector<uint8_t>(long accountId)> CharacterListBodyProvider;
    static std::function<uint64_t(long accountId, const std::vector<uint8_t>& createBody)> CreateCharacterProvider;
    static std::function<bool(long accountId, uint64_t characterId)> DeleteCharacterProvider;

    static bool SendAccountData;
    static bool SendRealmList;
    static bool IncludeRealm;
    static std::string RealmName;
    static std::string RealmHost;
    static uint32_t RealmPort;

    static std::vector<std::pair<uint16_t, std::vector<uint8_t>>> WorldEntrySequence;

    static void Register(net::GameServer& server);
    static void RegisterRealmConnection(net::GameServer& server);

    static std::vector<uint8_t> HelloBody();
};

} // namespace nexus::realm
