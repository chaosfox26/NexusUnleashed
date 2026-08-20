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
#include "proto/character_create.h"
#include "proto/game_data.h"

using namespace nexus;

int main() {
    std::setvbuf(stdout, nullptr, _IONBF, 0);   // unbuffered: log lines flush immediately
    realm::RealmConfig cfg = realm::RealmConfig::Load("realm.json");
    std::printf("=== %s realm host (C++) starting ===\n", cfg.realm_name.c_str());
    std::printf("bind %s | sts %u | auth %u | world %u\n",
                cfg.bind_address.c_str(), cfg.sts_port, cfg.auth_port, cfg.world_port);
    if (cfg.auth_database.empty()) { std::printf("FATAL: AuthDatabase not set in realm.json\n"); return 1; }

    // Client data tables (facts from the 16042 client, zero NF) drive the engine.
    size_t ccRows = proto::GameData::LoadCharacterCreation("data/character-creation.tsv");
    std::printf("game-data: CharacterCreation table loaded (%zu rows)\n", ccRows);

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
    // Create a character on 0x5CD5. The 298B body is decrypted but bit-packed; field parsing is
    // pending the message-definition table, so fields are placeholders for now (the name arrives
    // in a separate name-check). Goal: persist + let Enter Game advance into world entry.
    realm::WorldHandshake::CreateCharacterProvider = [&charStore](long acc, const std::vector<uint8_t>& body) -> uint64_t {
        db::NewCharacter nc;
        proto::CharacterCreateRequest req;
        bool parsed = proto::CharacterCreateRequest::Parse(body, req);
        nc.Name = (parsed && !req.Name.empty()) ? req.Name : "NexusHero";

        // Resolve race/class/sex/faction from the client's OWN CharacterCreation table (the
        // create packet only carries the creation ID). Authoritative, client-data-only.
        if (const auto* row = proto::GameData::Creation(req.CreationId)) {
            nc.Race = row->RaceId; nc.Class = row->ClassId;
            nc.Sex = row->Sex; nc.FactionId = row->FactionId;
            std::printf("realm: creationId %u -> race=%u class=%u sex=%u faction=%u start=%u\n",
                        req.CreationId, row->RaceId, row->ClassId, row->Sex, row->FactionId, row->StartEnum);
        } else {
            std::printf("realm: WARNING creationId %u not in CharacterCreation table; using defaults\n", req.CreationId);
            nc.Sex = 0; nc.Race = 1; nc.Class = 1; nc.FactionId = 167;
        }
        nc.ActivePath = 0;   // TODO: path is a separate field still to pin
        nc.WorldId = 0; nc.WorldZoneId = 0;   // TODO: from startEnum -> starting zone
        uint64_t id = charStore.CreateCharacter(acc, nc);
        std::printf("realm: create-character provider: account %ld name='%s' race=%u class=%u -> new char id %llu\n",
                    acc, nc.Name.c_str(), nc.Race, nc.Class, (unsigned long long)id);
        return id;
    };
    net::GameServer realmServer(io, cfg.bind_address, cfg.auth_port, /*worldChannel=*/false);
    realm::WorldHandshake::Register(realmServer);
    realmServer.Start();
    std::printf("realm/auth server listening on %u\n", cfg.auth_port);

    // ---- load the recorded world-load burst (reproduce step for 0x07DD Enter Game) ----
    {
        std::FILE* wf = std::fopen("captures/world-entry-replay.bin", "rb");
        if (wf) {
            uint32_t count = 0;
            if (std::fread(&count, 4, 1, wf) == 1) {
                for (uint32_t i = 0; i < count; ++i) {
                    uint16_t op; uint32_t len;
                    if (std::fread(&op, 2, 1, wf) != 1) break;
                    if (std::fread(&len, 4, 1, wf) != 1) break;
                    std::vector<uint8_t> b(len);
                    if (len && std::fread(b.data(), 1, len, wf) != len) break;
                    realm::WorldHandshake::WorldEntrySequence.emplace_back(op, std::move(b));
                }
            }
            std::fclose(wf);
            std::printf("world-entry replay: loaded %zu messages\n",
                        realm::WorldHandshake::WorldEntrySequence.size());
        } else {
            std::printf("world-entry replay: captures/world-entry-replay.bin not found (Enter Game will no-op)\n");
        }
    }

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
