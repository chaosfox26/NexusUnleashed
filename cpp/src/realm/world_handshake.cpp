// NexusUnleashed - clean-room authored. See world_handshake.h. 1:1 with WorldHandshake.cs.
#include "realm/world_handshake.h"
#include "net/world_packet.h"
#include "proto/character_list.h"
#include "proto/account_realm.h"
#include "sts/auth_flow.h"        // AuthSession
#include <cstdio>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>

namespace {
// Experimentation injector: inject.txt lines "<opcodeHex> <bodyHex>" are sent as CLEAR
// frames on realm-enter (before the char list). Lets us probe the account-retrieval
// handshake without rebuilding. Absent file = no-op.
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
}

using asio::awaitable;

namespace nexus::realm {

std::function<std::vector<uint8_t>(long)> WorldHandshake::CharacterListBodyProvider;
bool WorldHandshake::SendAccountData = false;  // these break the realm connection if sent at the
bool WorldHandshake::SendRealmList = false;    // "Connecting to realm" stage; hold until the right step.
bool WorldHandshake::IncludeRealm = true;
std::string WorldHandshake::RealmName = "NexusUnleashed";
std::string WorldHandshake::RealmHost = "127.0.0.1";
uint32_t WorldHandshake::RealmPort = 24000;

// The captured 0x0003 hello body (47 bytes after the opcode).
std::vector<uint8_t> WorldHandshake::HelloBody() {
    // Byte-for-byte the C# HelloBodyHex (WorldHandshake.cs): the 0b14332f01 stamp sits
    // at byte 26. The client validates message definitions from this — a shifted stamp
    // is "Message Definitions Mismatch".
    static const uint8_t b[] = {
        0xaa,0x3e,0x00,0x00,0x01,0x00,0x00,0x00,0x15,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
        0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x0b,0x14,0x33,0x2f,0x01,0x00,
        0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00
    };
    return std::vector<uint8_t>(b, b + sizeof(b));
}

void WorldHandshake::Register(net::GameServer& server) {
    server.on_connected = [](net::GameSession& s) -> awaitable<void> {
        // Auth/realm channel: the client accepts a CLEAR 0x0003 hello, then speaks the
        // auth-key container protocol. Send clear, then switch to container mode.
        std::printf("realm: client connected %s - clear 0x0003 hello, then container mode\n", s.remote().c_str());
        co_await s.SendClearGameMessage(0x0003, HelloBody());
        s.crypt.emplace(net::WorldPacket::WorldChannelSeed);
        co_return;
    };

    // Client realm-enter (token-bearing). Serve the char list (validated wire format).
    server.On(0x0592, [](net::GameSession& s, const std::vector<uint8_t>& body) -> awaitable<void> {
        std::printf("realm: <- 0x0592 realm-enter (%zuB)\n", body.size());

        // Realm-hello RESPONSE: opcode 0x0591 (u32, bit0 = flag). The client's connection
        // dispatcher (WS+0x370D0) advances the connection from state 6 -> state 9 on receiving
        // this (guard: state must be 6 or 8, which is exactly where it parks). Without it the
        // connection never completes and the account state never arms. Derived from the client.
        {
            std::vector<uint8_t> b = { 0x01, 0x00, 0x00, 0x00 }; // u32=1 (bit0 set)
            co_await s.SendGameMessage(0x0591, b);   // 0x76 container (PROVEN: decrypts + reaches
            std::printf("realm: -> 0x0591 realm-hello response (0x76 container; conn 6->9)\n");
        }
        // 0x3db: connection handshake step 2 (at state 9) -> installs the SECOND (0x3dc) cipher,
        // state 9->10.
        {
            auto b3db = proto::AccountRealmMessages::Build3db();
            co_await s.SendGameMessage(0x03db, b3db);
            std::printf("realm: -> 0x03db conn handshake step 2 (0x76 container; conn 9->10, %zuB)\n", b3db.size());
        }
        // NOTE: connection completion (op-3 on the realm lane) does NOT happen over this socket —
        // the realm lane is its OWN connection. After 0x3db the client dials the realm address we
        // put in the 0x3db body (127.0.0.1:world_port) and the char-select handshake happens there.

        // Experimentation: inject probe messages (inject.txt) before the char list.
        for (const auto& m : LoadInject()) {
            co_await s.SendClearGameMessage(m.opcode, m.body);
            std::printf("realm: -> [inject] 0x%04X (%zuB)\n", m.opcode, m.body.size());
        }

        // Account-retrieval handshake: advance past "Retrieving Account Information".
        //   0x7A1 account data -> account state 1->2
        //   0x761 realm list   -> fires RealmListChanged + NetworkStatus(nil) => overlay clears,
        //                         client advances to RealmSelect. Empty list still advances.
        // Realm-channel S->C is CLEAR framing (PROVEN LIVE 2026-08-20): the client's inner-msg
        // Read runs on a clear 0x761 (eax=0) but NOT on a 0x03DC container (client can't decode
        // our S->C container). So encryption/container is a red herring here; the block is that
        // the connection handshake never completes, so the account state never arms to dispatch.
        // Account/realm/char data go via the SECOND (0x03DC) container, whose decrypt cipher the
        // client installs when it processes 0x3db (state 9->10). The connection handshake (0x591,
        // 0x3db) uses 0x76; account data uses 0x03DC (a different channel -> the account state).
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
                r.PvpType = 0; r.Status = 0; r.Population = 0;   // PvE / Up / Low
                r.Host = RealmHost; r.AddrField10 = RealmPort;   // reconnect target (NEEDS LIVE VERIFY)
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
        // The connection object is already at state 10 (set by 0x3db on the auth socket); this
        // socket is bound to its realm lane (channel index 1). A CLEAR 0x0003 takes the client's
        // hello path, which validates against a fixed channel id and is dropped on the realm lane
        // (proven: it never reached the connection dispatcher). An ENCRYPTED 0x76 container routes
        // through the multi-lane dispatch, matches the realm lane, and op-3 hits sub_140038120 —
        // which creates the account object and completes the connection (state 10->11).
        s.crypt.emplace(net::WorldPacket::WorldChannelSeed);
        std::printf("realm-conn: -> 0x0003 (encrypted 0x76 container) on realm lane\n");
        co_await s.SendGameMessage(0x0003, HelloBody());
        // The client is now at char-select ("Retrieving Characters"). Serve the account's
        // character list (0x0117). Empty list -> the "Create Character" button (char creator).
        long acc = sts::AuthSession::LastAccountId();
        if (CharacterListBodyProvider) {
            auto charBody = CharacterListBodyProvider(acc);
            co_await s.SendGameMessage(proto::CharacterListMessage::Opcode, charBody);
            std::printf("realm-conn: -> 0x0117 character list for account %ld (%zuB)\n", acc, charBody.size());
        }
        co_return;
    };
    server.on_unhandled = [](net::GameSession&, uint16_t op, const std::vector<uint8_t>& body) {
        std::printf("realm-conn: <- op=0x%04X (%zuB)\n", op, body.size());
    };
}

} // namespace nexus::realm
