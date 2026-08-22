#include <cstdio>
#include <cstdlib>
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
    std::setvbuf(stdout, nullptr, _IONBF, 0);
    realm::RealmConfig cfg = realm::RealmConfig::Load("realm.json");
    std::printf("=== %s realm host (C++) starting ===\n", cfg.realm_name.c_str());
    std::printf("bind %s | sts %u | auth %u | world %u\n",
                cfg.bind_address.c_str(), cfg.sts_port, cfg.auth_port, cfg.world_port);
    realm::WorldHandshake::RealmName = cfg.realm_name;
    std::printf("realm name: \"%s\"\n", cfg.realm_name.c_str());
    if (cfg.auth_database.empty()) { std::printf("FATAL: AuthDatabase not set in realm.json\n"); return 1; }

    size_t ccRows = proto::GameData::LoadCharacterCreation("data/character-creation.tsv");
    std::printf("game-data: CharacterCreation table loaded (%zu rows)\n", ccRows);
    size_t cuRows = proto::GameData::LoadCharacterCustomization("data/character-customization.tsv");
    std::printf("game-data: CharacterCustomization table loaded (%zu rows)\n", cuRows);

    asio::io_context io;

    db::DbAccountStore accounts(cfg.auth_database);
    sts::StsServer stsServer(io, cfg.bind_address, cfg.sts_port);
    stsServer.request_observer = [](const sts::StsRequest& r) {
        std::printf("sts: <- %s %s (%zuB body)\n", r.method.c_str(), r.uri.c_str(), r.body.size());
    };
    sts::AuthFlow::Register(stsServer, accounts);
    stsServer.Start();
    std::printf("sts login server listening on %u\n", cfg.sts_port);

    db::DbCharacterStore charStore(cfg.auth_database);
    realm::WorldHandshake::CharacterListBodyProvider = [&charStore](long acc) {
        auto chars = charStore.GetCharacters(acc);
        std::printf("realm: character-list provider: account %ld has %zu character(s)\n", acc, chars.size());
        return proto::CharacterListMessage::Build(chars);
    };
    realm::WorldHandshake::CreateCharacterProvider = [&charStore](long acc, const std::vector<uint8_t>& body) -> uint64_t {
        db::NewCharacter nc;
        proto::CharacterCreateRequest req;
        bool parsed = proto::CharacterCreateRequest::Parse(body, req);
        nc.Name = (parsed && !req.Name.empty()) ? req.Name : "NexusHero";

        if (const auto* row = proto::GameData::Creation(req.CreationId)) {
            nc.Race = row->RaceId; nc.Class = row->ClassId;
            nc.Sex = row->Sex; nc.FactionId = row->FactionId;
            std::printf("realm: creationId %u -> race=%u class=%u sex=%u faction=%u start=%u\n",
                        req.CreationId, row->RaceId, row->ClassId, row->Sex, row->FactionId, row->StartEnum);
        } else {
            std::printf("realm: WARNING creationId %u not in CharacterCreation table; using defaults\n", req.CreationId);
            nc.Sex = 0; nc.Race = 1; nc.Class = 1; nc.FactionId = 167;
        }
        nc.ActivePath = 0;
        nc.WorldId = 0; nc.WorldZoneId = 0;
        nc.Customization = req.Customization;
        uint64_t id = charStore.CreateCharacter(acc, nc);
        std::printf("realm: create-character provider: account %ld name='%s' race=%u class=%u sliders=%zu -> new char id %llu\n",
                    acc, nc.Name.c_str(), nc.Race, nc.Class, nc.Customization.size(), (unsigned long long)id);
        return id;
    };
    realm::WorldHandshake::DeleteCharacterProvider = [&charStore](long acc, uint64_t charId) -> bool {
        bool ok = charStore.DeleteCharacter(acc, charId);
        std::printf("realm: delete-character provider: account %ld char %llu -> %s\n",
                    acc, (unsigned long long)charId, ok ? "deleted" : "not found");
        return ok;
    };
    realm::WorldHandshake::WorldEntryAppearanceProvider = [&charStore](long acc, uint64_t charId) -> proto::PlayerAppearance {
        proto::PlayerAppearance ap;   // defaults (Peryanna) if the character can't be found
        for (const auto& r : charStore.GetCharacters(acc)) {
            if (r.Id != charId) continue;
            ap.Race = r.Race; ap.Class = r.Class; ap.Sex = r.Sex;
            // ap.Faction stays the entity-construction faction (166), NOT r.FactionId — the DB
            // factionId (167 for Exiles) is a display value; construction needs the +272-installing key.
            ap.Name.assign(r.Name.begin(), r.Name.end());   // names are ASCII -> widen byte-by-byte
            ap.Visuals.clear();
            for (const auto& v : r.Appearance)              // {slot, displayId} from character_appearance
                ap.Visuals.emplace_back((uint16_t)v.first, (uint16_t)v.second);
            std::printf("realm: world-entry appearance: char %llu race=%u class=%u sex=%u visuals=%zu\n",
                        (unsigned long long)charId, ap.Race, ap.Class, ap.Sex, ap.Visuals.size());
            return ap;
        }
        std::printf("realm: world-entry appearance: char %llu NOT FOUND for account %ld, using defaults\n",
                    (unsigned long long)charId, acc);
        return ap;
    };
    realm::WorldHandshake::WorldEntryItemsProvider =
        [&charStore](uint64_t charId) -> std::vector<realm::WorldHandshake::WorldEntryItem> {
        std::vector<realm::WorldHandshake::WorldEntryItem> out;
        for (const auto& it : charStore.GetCharacterItems(charId))
            out.push_back({ it.ItemId, it.Location, it.BagIndex, it.StackCount, it.Durability });
        std::printf("realm: world-entry items: char %llu -> %zu item(s)\n",
                    (unsigned long long)charId, out.size());
        return out;
    };
    net::GameServer realmServer(io, cfg.bind_address, cfg.auth_port, false);
    realm::WorldHandshake::Register(realmServer);
    realmServer.Start();
    std::printf("realm/auth server listening on %u\n", cfg.auth_port);

    net::GameServer realmConn(io, cfg.bind_address, cfg.world_port, false);
    realm::WorldHandshake::RegisterRealmConnection(realmConn);
    // Persist live character state (position/world) when the world connection drops -> gear,
    // appearance AND position now survive across logins (server-side persistence).
    realmConn.on_disconnect = [&charStore](net::GameSession& s) {
        if (s.we_charid == 0) return;
        bool ok = charStore.UpdateCharacterState(s.we_charid, s.we_world,
                                                 s.we_has_pos, s.we_x, s.we_y, s.we_z);
        std::printf("realm: disconnect save char %llu world=%u pos=%s(%.2f,%.2f,%.2f) -> %s\n",
                    (unsigned long long)s.we_charid, s.we_world,
                    s.we_has_pos ? "" : "(none)", s.we_x, s.we_y, s.we_z, ok ? "OK" : "FAILED");
    };
    realmConn.Start();
    std::printf("realm connection server listening on %u\n", cfg.world_port);

    unsigned n = std::max(2u, std::thread::hardware_concurrency());
    if (const char* t = std::getenv("NUSL_THREADS")) {
        int req = std::atoi(t);
        if (req >= 1) n = (unsigned)req;
    }
    std::printf("worker pool: %u threads\n", n);
    std::vector<std::thread> pool;
    for (unsigned i = 1; i < n; ++i) pool.emplace_back([&io]{ io.run(); });
    io.run();
    for (auto& t : pool) t.join();
    return 0;
}
