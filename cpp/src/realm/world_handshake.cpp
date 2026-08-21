#include "realm/world_handshake.h"
#include "net/world_packet.h"
#include "proto/character_list.h"
#include "proto/character_create.h"
#include "proto/account_realm.h"
#include "sts/auth_flow.h"
#include <cstdio>
#include <cstring>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>

namespace {
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
        // World-entry messages are generated by hand from the client's own deserializers +
        // our DB, per-character. Built up message by message; nothing replayed.
        co_return;
    });

    server.on_unhandled = [](net::GameSession&, uint16_t op, const std::vector<uint8_t>& body) {
        std::printf("realm-conn: <- op=0x%04X (%zuB)\n%s\n", op, body.size(), HexDump(body).c_str());
    };
}

} // namespace nexus::realm
