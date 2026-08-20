// NexusUnleashed - clean-room authored. C++ port of WorldHandshake.cs. The realm/auth
// channel: a CLEAR 0x0003 hello on connect, then container mode; on 0x0592 realm-enter,
// serve the authenticated account's 0x0117 character list (CLEAR frame — the validated
// realm-channel S->C framing). The char-list body comes from a host-wired provider so the
// networking layer stays free of a DB dependency.
#pragma once
#include <cstdint>
#include <functional>
#include <utility>
#include <vector>
#include "net/game_server.h"

namespace nexus::realm {

struct WorldHandshake {
    // Wired by the host: accountId -> the 0x0117 body (from characterdb).
    static std::function<std::vector<uint8_t>(long accountId)> CharacterListBodyProvider;

    // Wired by the host: (accountId, decrypted 0x5CD5 body) -> new character id (0 on failure).
    // Persists the character; the realm-conn 0x5CD5 handler then refreshes the list and sends
    // the 0xDC create-result. See spec/protocol/character-create-0xDC.md.
    static std::function<uint64_t(long accountId, const std::vector<uint8_t>& createBody)> CreateCharacterProvider;

    // Account-retrieval handshake (spec/protocol/realm-list-0x761-and-account-0x7A1.md):
    // on realm-enter we push 0x7A1 (account data) then 0x761 (realm list) to clear the
    // "Retrieving Account Information" overlay and advance to RealmSelect. Configurable so
    // the empty-list vs one-realm test can be flipped without touching the handler.
    static bool SendAccountData;   // push 0x7A1 (default true)
    static bool SendRealmList;     // push 0x761 (default true)
    static bool IncludeRealm;      // 0x761 carries our realm entry vs empty list (default true)
    static std::string RealmName;  // strName
    static std::string RealmHost;  // reconnect host (NEEDS LIVE VERIFY)
    static uint32_t RealmPort;     // reconnect numeric (NEEDS LIVE VERIFY)

    // The recorded world-load burst (the reproduce step): a list of (innerOpcode, body) the
    // server streams on 0x07DD (Enter Game). Loaded from captures/world-entry-replay.bin.
    static std::vector<std::pair<uint16_t, std::vector<uint8_t>>> WorldEntrySequence;

    static void Register(net::GameServer& server);

    // The REALM connection (client dials 127.0.0.1:world_port after 0x3db). On this socket a
    // clear 0x0003 routes to the client's char-select handler (sub_140038120), not the login
    // hello — it creates the account object and moves toward the character screen.
    static void RegisterRealmConnection(net::GameServer& server);

    // The captured 0x0003 hello body (47 bytes). Public so the realm connection can reuse it.
    static std::vector<uint8_t> HelloBody();
};

} // namespace nexus::realm
