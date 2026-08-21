#include "realm/world_handshake.h"
#include "net/world_packet.h"
#include "proto/character_list.h"
#include "proto/character_create.h"
#include "proto/account_realm.h"
#include "proto/world_entry.h"
#include "sts/auth_flow.h"
#include <cstdio>
#include <cstring>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>

namespace {
// Toggle for the premature 0x36A world-change-done experiment. OFF: stay in the client's
// loading state (keeps sending 0x038C, connection stable) so we can find the true completion.
static bool WorldChangeDoneEnabled = false; // 0x36A CONFIRMED HARMFUL: forces game screen early ->
                                            // client disconnects 'Reason 0'. Stable state = keepalive
                                            // loading. Completion must come from the load finishing.
static bool LoadProgressEnabled = true;   // 0x845 progress/keepalive each tick after set-player
// TARGET WORLD — EXPERIMENT: 990 (Map\Eastern / Everstar Grove, a normal open zone) at a REAL valid
// spawn (realm world-DB entity), to test whether a plain entry completes in a non-tutorial world
// (1537 = ExileArkShipTutorial is scripted). Revert to 1537 / (1437.82,85.53,-106.82) after.
static uint32_t TWID = 1537;
static float TWX = 1437.82f, TWY = 85.53f, TWZ = -106.82f;
// (Tested world 990 Everstar Grove at a real spawn: NORMAL zone loading screen, connected, but ALSO
//  stalls at loading -> the completion blocker is GENERAL, not tutorial-specific. Reverted to 1537.)
struct InjectMsg { uint16_t opcode; std::vector<uint8_t> body; };
static std::vector<InjectMsg> LoadInject() {
    std::vector<InjectMsg> out;
    std::ifstream f("inject.txt");
    if (!f) return out;
    std::string line;
    while (std::getline(f, line)) {
        if (line.empty() || line[0] == '#') continue;
        std::istringstream ss(line);
        std::string opHex, bodyHex;
        ss >> opHex >> bodyHex;
        if (opHex.empty()) continue;
        InjectMsg m; m.opcode = (uint16_t)std::stoul(opHex, nullptr, 16);
        for (size_t i = 0; i + 1 < bodyHex.size(); i += 2)
            m.body.push_back((uint8_t)std::stoul(bodyHex.substr(i, 2), nullptr, 16));
        out.push_back(std::move(m));
    }
    return out;
}

static std::string HexDump(const std::vector<uint8_t>& b) {
    static const char* H = "0123456789abcdef";
    std::string out;
    for (size_t off = 0; off < b.size(); off += 16) {
        char pfx[10];
        std::snprintf(pfx, sizeof(pfx), "%04zx  ", off);
        out += pfx;
        std::string ascii;
        for (size_t i = 0; i < 16; ++i) {
            if (off + i < b.size()) {
                uint8_t c = b[off + i];
                out += H[c >> 4]; out += H[c & 0xF]; out += ' ';
                ascii += (c >= 0x20 && c < 0x7f) ? (char)c : '.';
            } else {
                out += "   ";
            }
            if (i == 7) out += ' ';
        }
        out += " |" + ascii + "|\n";
    }
    return out;
}
}

using asio::awaitable;

namespace nexus::realm {

std::function<std::vector<uint8_t>(long)> WorldHandshake::CharacterListBodyProvider;
std::function<uint64_t(long, const std::vector<uint8_t>&)> WorldHandshake::CreateCharacterProvider;
std::function<bool(long, uint64_t)> WorldHandshake::DeleteCharacterProvider;
bool WorldHandshake::SendAccountData = false;
bool WorldHandshake::SendRealmList = false;
bool WorldHandshake::IncludeRealm = true;
std::string WorldHandshake::RealmName = "Evindra";
std::string WorldHandshake::RealmHost = "127.0.0.1";
uint32_t WorldHandshake::RealmPort = 24000;

std::vector<uint8_t> WorldHandshake::HelloBody() {
    static const uint8_t b[] = {
        0xaa,0x3e,0x00,0x00,0x01,0x00,0x00,0x00,0x15,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
        0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x0b,0x14,0x33,0x2f,0x01,0x00,
        0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00
    };
    return std::vector<uint8_t>(b, b + sizeof(b));
}

void WorldHandshake::Register(net::GameServer& server) {
    server.on_connected = [](net::GameSession& s) -> awaitable<void> {
        std::printf("realm: client connected %s - clear 0x0003 hello, then container mode\n", s.remote().c_str());
        co_await s.SendClearGameMessage(0x0003, HelloBody());
        s.crypt.emplace(net::WorldPacket::WorldChannelSeed);
        co_return;
    };

    server.On(0x0592, [](net::GameSession& s, const std::vector<uint8_t>& body) -> awaitable<void> {
        std::printf("realm: <- 0x0592 realm-enter (%zuB)\n", body.size());

        {
            std::vector<uint8_t> b = { 0x01, 0x00, 0x00, 0x00 };
            co_await s.SendGameMessage(0x0591, b);
            std::printf("realm: -> 0x0591 realm-hello response (0x76 container; conn 6->9)\n");
        }
        {
            auto b3db = proto::AccountRealmMessages::Build3db();
            co_await s.SendGameMessage(0x03db, b3db);
            std::printf("realm: -> 0x03db conn handshake step 2 (0x76 container; conn 9->10, %zuB)\n", b3db.size());
        }

        for (const auto& m : LoadInject()) {
            co_await s.SendClearGameMessage(m.opcode, m.body);
            std::printf("realm: -> [inject] 0x%04X (%zuB)\n", m.opcode, m.body.size());
        }

        if (SendAccountData) {
            auto acctBody = proto::AccountRealmMessages::BuildAccountData();
            co_await s.SendGameMessageVia(0x03DC, proto::AccountRealmMessages::OpAccountData, acctBody);
            std::printf("realm: -> 0x07A1 account data (0x3dc container, %zuB)\n", acctBody.size());
        }
        if (SendRealmList) {
            std::vector<proto::RealmEntry> realms;
            if (IncludeRealm) {
                proto::RealmEntry r;
                r.Id = 1; r.Name = RealmName;
                r.PvpType = 2; r.Status = 4; r.Population = 0;
                r.Host = RealmHost; r.AddrField10 = RealmPort;
                realms.push_back(r);
            }
            auto realmBody = proto::AccountRealmMessages::BuildRealmList(realms);
            co_await s.SendGameMessageVia(0x03DC, proto::AccountRealmMessages::OpRealmList, realmBody);
            std::printf("realm: -> 0x0761 realm list, %zu realm(s) (0x3dc container, %zuB)\n", realms.size(), realmBody.size());
        }

        long acc = sts::AuthSession::LastAccountId();
        if (CharacterListBodyProvider && SendRealmList) {
            std::vector<uint8_t> charBody = CharacterListBodyProvider(acc);
            co_await s.SendGameMessageVia(0x03DC, proto::CharacterListMessage::Opcode, charBody);
            std::printf("realm: -> 0x0117 character list (0x3dc container) for account %ld (%zuB)\n",
                        acc, charBody.size());
        } else {
            std::printf("realm: no character-list provider wired\n");
        }
        co_return;
    });

    server.on_unhandled = [](net::GameSession&, uint16_t op, const std::vector<uint8_t>& body) {
        std::printf("realm: <- inner op=0x%04X (%zuB)\n", op, body.size());
    };
}

void WorldHandshake::RegisterRealmConnection(net::GameServer& server) {
    server.on_connected = [](net::GameSession& s) -> awaitable<void> {
        s.crypt.emplace(net::WorldPacket::WorldChannelSeed);
        std::printf("realm-conn: -> 0x0003 (encrypted 0x76 container) on realm lane\n");
        co_await s.SendGameMessage(0x0003, HelloBody());
        long acc = sts::AuthSession::LastAccountId();
        if (CharacterListBodyProvider) {
            auto charBody = CharacterListBodyProvider(acc);
            co_await s.SendGameMessage(proto::CharacterListMessage::Opcode, charBody);
            std::printf("realm-conn: -> 0x0117 character list for account %ld (%zuB)\n", acc, charBody.size());
        }
        co_return;
    };

    server.On(0x058F, [](net::GameSession& s, const std::vector<uint8_t>& body) -> awaitable<void> {
        std::printf("realm-conn: <- 0x058F realm-enter (%zuB) -> RE-KEY channel to RealmLaneKey\n", body.size());
        s.crypt.emplace(net::WorldPacket::RealmLaneKey);
        co_return;
    });

    server.On(0x07A4, [](net::GameSession& s, const std::vector<uint8_t>&) -> awaitable<void> {
        std::vector<proto::RealmEntry> realms;
        proto::RealmEntry r;
        r.Id = 1; r.Name = RealmName;
        r.PvpType = 2; r.Status = 4; r.Population = 0;
        r.Host = RealmHost; r.AddrField10 = RealmPort;
        realms.push_back(r);
        auto realmBody = proto::AccountRealmMessages::BuildRealmList(realms);
        co_await s.SendGameMessageVia(0x03DC, proto::AccountRealmMessages::OpRealmList, realmBody);
        std::printf("realm-conn: -> 0x0761 realm list (\"%s\") via 0x03DC on 0x07A4 request (%zuB)\n",
                    RealmName.c_str(), realmBody.size());
        co_return;
    });

    server.On(0x07DF, [](net::GameSession& s, const std::vector<uint8_t>&) -> awaitable<void> {
        long acc = sts::AuthSession::LastAccountId();
        if (CharacterListBodyProvider) {
            auto charBody = CharacterListBodyProvider(acc);
            co_await s.SendGameMessageVia(0x03DC, proto::CharacterListMessage::Opcode, charBody);
            std::printf("realm-conn: -> 0x0117 char list via 0x03DC on 0x07DF realm-enter (%zuB)\n", charBody.size());
        }
        co_return;
    });

    server.On(proto::CharacterCreateRequest::Opcode, [](net::GameSession& s, const std::vector<uint8_t>& body) -> awaitable<void> {
        std::printf("realm-conn: <- 0x025C CharacterCreate (%zuB)\n%s\n",
                    body.size(), HexDump(body).c_str());
        long acc = sts::AuthSession::LastAccountId();

        uint64_t newId = 0;
        if (CreateCharacterProvider) newId = CreateCharacterProvider(acc, body);

        if (newId == 0) {
            co_await s.SendGameMessageVia(0x03DC, proto::CharacterCreateResult::Opcode,
                proto::CharacterCreateResult::Build(0, proto::CharacterCreateResult::GenericFail));
            std::printf("realm-conn: -> 0x00DC create result FAIL via 0x03DC (no id)\n");
            co_return;
        }

        if (CharacterListBodyProvider) {
            auto charBody = CharacterListBodyProvider(acc);
            co_await s.SendGameMessageVia(0x03DC, proto::CharacterListMessage::Opcode, charBody);
            std::printf("realm-conn: -> 0x0117 refreshed character list via 0x03DC (%zuB)\n", charBody.size());
        }
        co_await s.SendGameMessageVia(0x03DC, proto::CharacterCreateResult::Opcode,
            proto::CharacterCreateResult::Build(newId, proto::CharacterCreateResult::Ok));
        std::printf("realm-conn: -> 0x00DC create result OK via 0x03DC, new char id %llu\n",
                    (unsigned long long)newId);
        co_return;
    });

    server.On(0x0352, [](net::GameSession& s, const std::vector<uint8_t>& body) -> awaitable<void> {
        uint64_t charId = 0;
        for (size_t i = 0; i < body.size() && i < 8; ++i) charId |= (uint64_t)body[i] << (8 * i);
        long acc = sts::AuthSession::LastAccountId();
        bool ok = DeleteCharacterProvider ? DeleteCharacterProvider(acc, charId) : false;
        std::printf("realm-conn: <- 0x0352 CharacterDelete charId=%llu (account %ld) -> %s\n",
                    (unsigned long long)charId, acc, ok ? "deleted" : "FAILED");

        std::vector<uint8_t> result(8, 0);
        uint32_t code = ok ? 0u : 1u;
        std::memcpy(result.data(), &code, 4);
        co_await s.SendGameMessageVia(0x03DC, 0x00E6, result);
        std::printf("realm-conn: -> 0x00E6 delete result via 0x03DC (code %u)\n", code);
        co_return;
    });

    server.On(0x07DD, [](net::GameSession& s, const std::vector<uint8_t>& body) -> awaitable<void> {
        uint64_t charId = 0;
        for (size_t i = 0; i < body.size() && i < 8; ++i) charId |= (uint64_t)body[i] << (8 * i);
        std::printf("realm-conn: <- 0x07DD EnterWorld charId=%llu\n", (unsigned long long)charId);

        // World entry, built by hand from the client's own deserializers + our DB (NF-free;
        // spec/protocol/world-entry.md). FIRST CANDIDATE: the leading messages with
        // client-derived shapes (empty lists) to observe whether the client leaves
        // char-select and enters its world-load state. Iterated from the client's reaction.
        using proto::WorldEntryMessages;

        // THE WORLD-ENTER: 0x00AD tells the client "load world W at (X,Y,Z)". Handler
        // sub_140022480 (state 4 only) transitions the client to state 5 = LOADING, on the
        // SAME connection (uses +184; no reconnect). TODO: per-character worldId/position via a
        // provider. Target = world 1537 (Map\ExileArkShipTutorial, the current tutorial zone),
        // spawn = a valid arkship deck position.
        uint32_t worldId = TWID;
        float wx = TWX, wy = TWY, wz = TWZ;
        auto bAD = WorldEntryMessages::BuildWorldEnter(worldId, wx, wy, wz);
        co_await s.SendGameMessageVia(0x03DC, WorldEntryMessages::OpWorldEnter, bAD);
        std::printf("realm-conn: -> 0x00AD WORLD-ENTER world=%u pos(%.1f,%.1f,%.1f) (%zuB) via 0x03DC\n",
                    worldId, wx, wy, wz, bAD.size());

        // NOTE: world-init (0x0988/0x098B/0x0981) is NOT sent here — at this point the client is
        // still on the char-select/realm side and drops them. They are sent in the 0x038C handler
        // AFTER the 2nd 0x00AD (ChangeWorld), when the world channel is live.
        co_return;
    });

    // The client's 0x038C movement carries its player unit guid (u64 at body+7). On the first
    // one after world-enter, send a 0x0262 entity-create for that guid so the client matches it
    // to its player, fires PlayerChanged, and drops off the loading screen.
    server.On(0x038C, [](net::GameSession& s, const std::vector<uint8_t>& body) -> awaitable<void> {
        if (body.size() < 43) co_return;   // only 43-byte position-bearing movement
        s.world_move_count++;
        uint64_t guid64 = 0;
        for (int i = 0; i < 8; ++i) guid64 |= (uint64_t)body[7 + i] << (8 * i);
        uint32_t guid = (uint32_t)guid64;
        if (guid == 0) co_return;

        if (!s.player_entity_sent) {
            s.player_entity_sent = true;
            // 1) 0x00AD (2nd) -> world dispatcher -> ChangeWorld (creates the game state / grid).
            auto bAD2 = proto::WorldEntryMessages::BuildWorldEnter(TWID, TWX, TWY, TWZ);
            co_await s.SendGameMessageVia(0x03DC, proto::WorldEntryMessages::OpWorldEnter, bAD2);
            std::printf("realm-conn: -> 0x00AD (2nd, world-load complete) world=%u via 0x03DC\n", TWID);
            // 1b) world-init set, NOW in the world context so the client actually dispatches them
            //     (world-scene/zone setup: 0x0988, 0x098B, 0x0981).
            { auto m = proto::WorldEntryMessages::Build0988Empty();
              co_await s.SendGameMessageVia(0x03DC, proto::WorldEntryMessages::Op0988, m); }
            { auto m = proto::WorldEntryMessages::Build098BEmpty();
              co_await s.SendGameMessageVia(0x03DC, proto::WorldEntryMessages::Op098B, m); }
            { auto m = proto::WorldEntryMessages::BuildWorldInit({});
              co_await s.SendGameMessageVia(0x03DC, proto::WorldEntryMessages::OpWorldInit, m); }
            std::printf("realm-conn: -> 0x0988/0x098B/0x0981 world-init (world context) via 0x03DC\n");
            // 2) 0x0262 entity-create. Must construct + land in the client's lookup map (this is the
            //    step currently failing: Read succeeds but construct/add does not).
            auto ent = proto::WorldEntryMessages::BuildPlayerEntity(guid, TWX, TWY, TWZ);
            co_await s.SendGameMessageVia(0x03DC, proto::WorldEntryMessages::OpEntityCreate, ent);
            std::printf("realm-conn: -> 0x0262 player entity guid=0x%X (%zuB) via 0x03DC\n", guid, ent.size());
        } else if (!s.player_set_sent && s.world_move_count >= 4) {
            // 3) 0x019B set-player (primary; sub_1403B5AD0): looks up the entity, sets BOTH the
            //    current player (+120) AND the container (+25744) from it, then fires PlayerChanged.
            //    Requires the entity to exist AND carry its +272 unit component.
            s.player_set_sent = true;
            auto bSet = proto::WorldEntryMessages::BuildSetPlayer(guid);
            co_await s.SendGameMessageVia(0x03DC, proto::WorldEntryMessages::OpSetPlayer, bSet);
            std::printf("realm-conn: -> 0x019B SET-PLAYER guid=0x%X (move #%d) via 0x03DC\n",
                        guid, s.world_move_count);
        } else if (!s.loadscreen_sent && s.player_set_sent && s.world_move_count >= 6) {
            // 4) 0x636 SET-PLAYER-UNIT (world-channel, sub_14057A630): the proper WORLD player bind
            //    (vs 0x019B, the char-select-mgr variant). Needs the container (+25744) which 0x019B
            //    now installs. This is the legitimate world-entry finalizer; testing whether it
            //    advances the stuck world-loader (state 2) to complete the load. [32b unit][1b][32b guid]
            s.loadscreen_sent = true;
            auto bUnit = proto::WorldEntryMessages::BuildSetPlayerUnit(guid, true);
            co_await s.SendGameMessageVia(0x03DC, proto::WorldEntryMessages::OpSetPlayerUnit, bUnit);
            std::printf("realm-conn: -> 0x636 SET-PLAYER-UNIT guid=0x%X (move #%d) via 0x03DC\n",
                        guid, s.world_move_count);
        }

        // KEEPALIVE / PROGRESS EXPERIMENT: once the player is set, send 0x845 loading progress on
        // each movement tick, ramping to 100%. Tests whether the ~25-30s post-world-enter drop is a
        // receive-timeout (no server world-traffic) and simultaneously fills the load bar.
        if (LoadProgressEnabled && s.player_set_sent) {
            uint32_t cur = s.world_move_count > 20 ? 20 : (uint32_t)s.world_move_count;
            auto bp = proto::WorldEntryMessages::BuildLoadProgress(cur, 0, 20);
            co_await s.SendGameMessageVia(0x03DC, proto::WorldEntryMessages::OpLoadProgress, bp);
            std::printf("realm-conn: -> 0x0845 load progress %u/20 (move #%d)\n", cur, s.world_move_count);
        }
        co_return;
    });

    server.on_unhandled = [](net::GameSession&, uint16_t op, const std::vector<uint8_t>& body) {
        std::printf("realm-conn: <- op=0x%04X (%zuB)\n%s\n", op, body.size(), HexDump(body).c_str());
    };
}

} // namespace nexus::realm
