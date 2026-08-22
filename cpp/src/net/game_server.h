#pragma once
#include <cstdint>
#include <functional>
#include <map>
#include <memory>
#include <optional>
#include <string>
#include <utility>
#include <vector>
#include <deque>
#include <asio.hpp>
#include "crypto/packet_crypt.h"

namespace nexus::net {

class GameServer;

class GameSession : public std::enable_shared_from_this<GameSession> {
public:
    GameSession(asio::ip::tcp::socket sock, GameServer& server, bool worldChannel);

    asio::awaitable<void> Run();

    asio::awaitable<void> SendClearGameMessage(uint16_t opcode, std::vector<uint8_t> body);
    asio::awaitable<void> SendGameMessage(uint16_t opcode, std::vector<uint8_t> body);
    asio::awaitable<void> SendGameMessageVia(uint16_t containerOpcode, uint16_t opcode, std::vector<uint8_t> body);

    // Serialized write path: all sends enqueue here so concurrent writers (dispatch + keepalive
    // timer) never interleave frames on the socket.
    asio::awaitable<void> WriteFrame(std::vector<uint8_t> frame);
    // Movement-independent keepalive: co_spawns a loop that re-sends (container,op,body) every
    // intervalMs so the client's world-entry watchdog keeps getting world-channel traffic even
    // after it stops sending movement (e.g. after a game-screen transition). Starts once.
    void StartKeepalive(uint16_t containerOpcode, uint16_t opcode, std::vector<uint8_t> body, int intervalMs);
    // One-shot delayed coroutine: co_spawns a timer that runs fn() once after delayMs.
    void SpawnDelayed(int delayMs, std::function<asio::awaitable<void>()> fn);

    std::optional<crypto::PacketCrypt> crypt;
    long account_id = 0;
    bool player_entity_sent = false;   // world entry: player 0x0262 sent once (on first 0x038C)
    int  world_move_count = 0;         // count of 43-byte movement packets seen (for staging)
    bool player_set_sent = false;      // world entry: 0x019B set-player sent (after entity exists)
    bool loadscreen_sent = false;      // world entry: 0x03D0 loading-screen dismiss sent
    bool worldchange_sent = false;     // world entry: 0x036A world-change-complete (game-screen) sent
    bool chardata_sent = false;        // world entry: 0x025E character-data blob sent (fires CharacterCreated)
    bool keepalive_stop = false;       // set to halt the keepalive entirely
    // The keepalive loop re-reads these each tick, so the keepalive MESSAGE can be switched live
    // (loading-progress 0x0845 before the game screen -> a gameplay-valid heartbeat 0x0935 after).
    uint16_t ka_container = 0, ka_op = 0;
    std::vector<uint8_t> ka_body;

    // world entry: the entering character's body, loaded from characterdb on 0x07DD and
    // used to build the per-character 0x0262 player entity on the first 0x038C movement.
    bool     we_loaded = false;
    uint32_t we_race = 4, we_class = 7, we_sex = 1, we_faction = 166;
    std::u16string we_name = u"Peryanna Meadowclover";
    std::vector<std::pair<uint16_t, uint16_t>> we_visuals; // {slot, displayId} from character_appearance
    // world entry: pre-built 0x111 item-add message bodies (equipped + inventory), loaded from
    // characterdb.item on 0x07DD and streamed to the client at move#4 so its cache has the gear
    // (equip slot 16 weapon -> action bar shows; other slots -> paperdoll / inventory).
    std::vector<std::vector<uint8_t>> we_item_msgs;
    // persistence: the entering character id (from 0x07DD) and the latest live position parsed
    // from the client's 0x038C movement stream, saved to characterdb on disconnect.
    uint64_t we_charid = 0;
    uint32_t we_world = 0;
    bool  we_has_pos = false;
    float we_x = 0.f, we_y = 0.f, we_z = 0.f;

    std::string remote() const;

private:
    asio::awaitable<void> Dispatch(uint16_t opcode, std::vector<uint8_t> payload);

    asio::ip::tcp::socket sock_;
    GameServer& server_;
    std::vector<uint8_t> buf_;
    std::deque<std::vector<uint8_t>> write_q_;
    bool writing_ = false;
    bool ka_started_ = false;
};

class GameServer {
public:
    using Handler   = std::function<asio::awaitable<void>(GameSession&, const std::vector<uint8_t>&)>;
    using Connected = std::function<asio::awaitable<void>(GameSession&)>;
    using Unhandled = std::function<void(GameSession&, uint16_t, const std::vector<uint8_t>&)>;
    using Disconnected = std::function<void(GameSession&)>;

    GameServer(asio::io_context& io, const std::string& address, uint16_t port, bool worldChannel);

    void On(uint16_t opcode, Handler h) { handlers_[opcode] = std::move(h); }
    Connected on_connected;
    Unhandled on_unhandled;
    Disconnected on_disconnect;   // fires when a session's socket closes (save character state)

    void Start();

    bool world_channel() const { return world_channel_; }
    asio::io_context& io() { return io_; }

private:
    friend class GameSession;
    asio::awaitable<void> AcceptLoop();

    asio::io_context& io_;
    asio::ip::tcp::acceptor acceptor_;
    bool world_channel_;
    std::map<uint16_t, Handler> handlers_;
};

} // namespace nexus::net
