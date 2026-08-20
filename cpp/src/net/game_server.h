// NexusUnleashed - clean-room authored. C++ port of GameServer.cs / GameSession.cs. Asio
// TCP acceptor that spins a session per connection; frames are length-prefixed; a client
// 0x0244 container (world/realm channel) is decrypted and its inner message dispatched.
// Handlers are keyed by opcode and are coroutines (they co_await sends). Unknown opcodes
// are logged, never fatal.
#pragma once
#include <cstdint>
#include <functional>
#include <map>
#include <memory>
#include <optional>
#include <vector>
#include <asio.hpp>
#include "crypto/packet_crypt.h"

namespace nexus::net {

class GameServer;

class GameSession : public std::enable_shared_from_this<GameSession> {
public:
    GameSession(asio::ip::tcp::socket sock, GameServer& server, bool worldChannel);

    asio::awaitable<void> Run();

    // S->C realm channel uses CLEAR frames; the encrypted container is world-channel.
    asio::awaitable<void> SendClearGameMessage(uint16_t opcode, std::vector<uint8_t> body);
    asio::awaitable<void> SendGameMessage(uint16_t opcode, std::vector<uint8_t> body);
    // Send in a container with an explicit container opcode (0x76 conn / 0x03DC account/world).
    asio::awaitable<void> SendGameMessageVia(uint16_t containerOpcode, uint16_t opcode, std::vector<uint8_t> body);

    std::optional<crypto::PacketCrypt> crypt;   // set on the realm/world channel
    long account_id = 0;                          // correlation (set from AuthSession)
    std::string remote() const;

private:
    asio::awaitable<void> Dispatch(uint16_t opcode, std::vector<uint8_t> payload);

    asio::ip::tcp::socket sock_;
    GameServer& server_;
    std::vector<uint8_t> buf_;
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
