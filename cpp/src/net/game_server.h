#pragma once
#include <cstdint>
#include <functional>
#include <map>
#include <memory>
#include <optional>
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

    std::optional<crypto::PacketCrypt> crypt;
    long account_id = 0;
    bool player_entity_sent = false;   // world entry: player 0x0262 sent once (on first 0x038C)
    int  world_move_count = 0;         // count of 43-byte movement packets seen (for staging)
    bool player_set_sent = false;      // world entry: 0x019B set-player sent (after entity exists)
    bool loadscreen_sent = false;      // world entry: 0x03D0 loading-screen dismiss sent
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

    GameServer(asio::io_context& io, const std::string& address, uint16_t port, bool worldChannel);

    void On(uint16_t opcode, Handler h) { handlers_[opcode] = std::move(h); }
    Connected on_connected;
    Unhandled on_unhandled;

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
