// NexusUnleashed - clean-room authored. Realm host entry point (C++ port of Program.cs).
// Boots the STS login server + the realm/auth channel; the world channel arrives later.
#include <cstdio>
#include <thread>
#include <vector>
#include <asio.hpp>
#include "realm/config.h"
#include "realm/world_handshake.h"
#include "sts/sts_server.h"
#include "sts/auth_flow.h"
#include "sts/sts_message.h"
#include "net/game_server.h"
#include "db/db_store.h"
#include "proto/character_list.h"

using namespace nexus;

int main() {
    std::setvbuf(stdout, nullptr, _IONBF, 0);   // unbuffered: log lines flush immediately
    realm::RealmConfig cfg = realm::RealmConfig::Load("realm.json");
    std::printf("=== %s realm host (C++) starting ===\n", cfg.realm_name.c_str());
    std::printf("bind %s | sts %u | auth %u | world %u\n",
                cfg.bind_address.c_str(), cfg.sts_port, cfg.auth_port, cfg.world_port);
    if (cfg.auth_database.empty()) { std::printf("FATAL: AuthDatabase not set in realm.json\n"); return 1; }

    asio::io_context io;

    // ---- STS login server ----
    db::DbAccountStore accounts(cfg.auth_database);
    sts::StsServer stsServer(io, cfg.bind_address, cfg.sts_port);
    stsServer.request_observer = [](const sts::StsRequest& r) {
        std::printf("sts: <- %s %s (%zuB body)\n", r.method.c_str(), r.uri.c_str(), r.body.size());
    };
    sts::AuthFlow::Register(stsServer, accounts);
    stsServer.Start();
    std::printf("sts login server listening on %u\n", cfg.sts_port);

    // ---- realm/auth channel (clear 0x0003 hello, then container) ----
    db::DbCharacterStore charStore(cfg.auth_database);
    realm::WorldHandshake::CharacterListBodyProvider = [&charStore](long acc) {
        auto chars = charStore.GetCharacters(acc);
        std::printf("realm: character-list provider: account %ld has %zu character(s)\n", acc, chars.size());
        return proto::CharacterListMessage::Build(chars);
    };
    net::GameServer realmServer(io, cfg.bind_address, cfg.auth_port, /*worldChannel=*/false);
    realm::WorldHandshake::Register(realmServer);
    realmServer.Start();
    std::printf("realm/auth server listening on %u\n", cfg.auth_port);

    // ---- realm CONNECTION (client dials 127.0.0.1:world_port after 0x3db → char-select) ----
    // The 0x3db handshake hands the client this address; it opens a NEW socket here. First we
    // just observe what it sends so we can answer with the op-3 that creates the char screen.
    net::GameServer realmConn(io, cfg.bind_address, cfg.world_port, /*worldChannel=*/false);
    realm::WorldHandshake::RegisterRealmConnection(realmConn);
    realmConn.Start();
    std::printf("realm connection server listening on %u\n", cfg.world_port);

    // ---- run the io_context across the hardware threads ----
    unsigned n = std::max(2u, std::thread::hardware_concurrency());
    std::vector<std::thread> pool;
    for (unsigned i = 1; i < n; ++i) pool.emplace_back([&io]{ io.run(); });
    io.run();
    for (auto& t : pool) t.join();
    return 0;
}
