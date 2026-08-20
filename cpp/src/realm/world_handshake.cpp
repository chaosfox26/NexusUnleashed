// NexusUnleashed - clean-room authored. See world_handshake.h. 1:1 with WorldHandshake.cs.
#include "realm/world_handshake.h"
#include "net/world_packet.h"
#include "proto/character_list.h"
#include "proto/character_create.h"
#include "proto/account_realm.h"
#include "sts/auth_flow.h"        // AuthSession
#include <cstdio>
#include <cstring>
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

// Canonical hex+ascii dump (16 bytes/row) for pinning unknown wire payloads offline.
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
std::vector<std::pair<uint16_t, std::vector<uint8_t>>> WorldHandshake::WorldEntrySequence;
bool WorldHandshake::SendAccountData = false;  // these break the realm connection if sent at the
bool WorldHandshake::SendRealmList = false;    // "Connecting to realm" stage; hold until the right step.
bool WorldHandshake::IncludeRealm = true;
std::string WorldHandshake::RealmName = "Evindra";  // overridden from realm.json at boot (main.cpp)
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
    // 0x058F = the client's realm-enter (token-bearing). It is the LAST message ciphered with
    // the auth key (WorldChannelSeed): right after sending it, the client re-keys its channel to
    // the fixed realm-lane key. So we re-key our session cipher here too — every C->S message
    // after this (the char-create bundle, world-enter requests) is RealmLaneKey, not WorldChannelSeed.
    // (This message itself was already decoded with the auth key before this handler ran.)
    server.On(0x058F, [](net::GameSession& s, const std::vector<uint8_t>& body) -> awaitable<void> {
        std::printf("realm-conn: <- 0x058F realm-enter (%zuB) -> RE-KEY channel to RealmLaneKey\n", body.size());
        s.crypt.emplace(net::WorldPacket::RealmLaneKey);
        co_return;
    });

    // 0x025C = ClientCharacterCreate (Enter Game on the creator's Finalize page). After the
    // realm-lane re-key the body is readable: [u32 total][u16 0x025B][u32 flags][wide name]
    // [u32 appearance...]. We parse the name, persist, refresh the list, and send the 0xDC result.
    server.On(proto::CharacterCreateRequest::Opcode, [](net::GameSession& s, const std::vector<uint8_t>& body) -> awaitable<void> {
        std::printf("realm-conn: <- 0x025C CharacterCreate (%zuB)\n%s\n",
                    body.size(), HexDump(body).c_str());
        long acc = sts::AuthSession::LastAccountId();

        // Persist the new character (host-wired). The name is NOT in this packet (the client
        // sends it in a separate name-check); fields are best-effort for now — the milestone
        // is making Enter Game advance into world entry.
        uint64_t newId = 0;
        if (CreateCharacterProvider) newId = CreateCharacterProvider(acc, body);

        if (newId == 0) {
            // Could not create → tell the client it failed rather than hang.
            co_await s.SendGameMessage(proto::CharacterCreateResult::Opcode,
                proto::CharacterCreateResult::Build(0, proto::CharacterCreateResult::GenericFail));
            std::printf("realm-conn: -> 0x00DC create result FAIL (no id)\n");
            co_return;
        }

        // Success: refresh the character list so the client learns the new character, then send
        // the 0xDC result (code 3). The client looks the new char up in its list and enters world.
        if (CharacterListBodyProvider) {
            auto charBody = CharacterListBodyProvider(acc);
            co_await s.SendGameMessage(proto::CharacterListMessage::Opcode, charBody);
            std::printf("realm-conn: -> 0x0117 refreshed character list (%zuB)\n", charBody.size());
        }
        co_await s.SendGameMessage(proto::CharacterCreateResult::Opcode,
            proto::CharacterCreateResult::Build(newId, proto::CharacterCreateResult::Ok));
        std::printf("realm-conn: -> 0x00DC create result OK, new char id %llu\n",
                    (unsigned long long)newId);
        co_return;
    });

    // 0x0352 = ClientCharacterDelete (the char-select Delete button). Client sends message 850
    // with body = u64 characterId (sub_140024C10). We soft-delete it, then send the 0xE6 result;
    // the client's dispatcher (sub_140020EA0, opcode 230) removes the character from its list when
    // result == 0, which frees the slot. Body is two byte-aligned u32s {result, 0}.
    server.On(0x0352, [](net::GameSession& s, const std::vector<uint8_t>& body) -> awaitable<void> {
        uint64_t charId = 0;
        for (size_t i = 0; i < body.size() && i < 8; ++i) charId |= (uint64_t)body[i] << (8 * i);
        long acc = sts::AuthSession::LastAccountId();
        bool ok = DeleteCharacterProvider ? DeleteCharacterProvider(acc, charId) : false;
        std::printf("realm-conn: <- 0x0352 CharacterDelete charId=%llu (account %ld) -> %s\n",
                    (unsigned long long)charId, acc, ok ? "deleted" : "FAILED");

        std::vector<uint8_t> result(8, 0);
        uint32_t code = ok ? 0u : 1u;              // 0 = removed (client drops it); nonzero = fail
        std::memcpy(result.data(), &code, 4);
        co_await s.SendGameMessage(0x00E6, result);
        std::printf("realm-conn: -> 0x00E6 delete result (code %u)\n", code);
        co_return;
    });

    // 0x07DD = Enter Game on a selected character (body = u64 characterId). This is the world-
    // entry trigger. First reproduce step: stream the recorded world-load burst (0x0988, zone
    // blobs, player self, entity spawns) so the client leaves char-select and loads the world.
    // Sent via the 0x03DC world container (matching the capture) on the current cipher.
    server.On(0x07DD, [](net::GameSession& s, const std::vector<uint8_t>& body) -> awaitable<void> {
        uint64_t charId = 0;
        for (size_t i = 0; i < body.size() && i < 8; ++i) charId |= (uint64_t)body[i] << (8 * i);
        std::printf("realm-conn: <- 0x07DD EnterWorld charId=%llu -> replaying %zu world messages\n",
                    (unsigned long long)charId, WorldHandshake::WorldEntrySequence.size());
        size_t sent = 0, bytes = 0;
        for (const auto& m : WorldHandshake::WorldEntrySequence) {
            co_await s.SendGameMessageVia(0x03DC, m.first, m.second);
            ++sent; bytes += m.second.size();
        }
        std::printf("realm-conn: -> world-entry replay complete (%zu msgs, %zu body bytes)\n", sent, bytes);
        co_return;
    });

    server.on_unhandled = [](net::GameSession&, uint16_t op, const std::vector<uint8_t>& body) {
        std::printf("realm-conn: <- op=0x%04X (%zuB)\n%s\n", op, body.size(), HexDump(body).c_str());
    };
}

} // namespace nexus::realm
