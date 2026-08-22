#include "net/game_server.h"
#include "net/frame.h"
#include "net/world_packet.h"
#include <cstdio>
#include <chrono>

using asio::ip::tcp;
using asio::awaitable;
using asio::co_spawn;
using asio::detached;
using asio::use_awaitable;

namespace nexus::net {

GameSession::GameSession(tcp::socket sock, GameServer& server, bool)
    : sock_(std::move(sock)), server_(server) {}

std::string GameSession::remote() const {
    asio::error_code ec; auto ep = sock_.remote_endpoint(ec);
    return ec ? "?" : (ep.address().to_string() + ":" + std::to_string(ep.port()));
}

awaitable<void> GameSession::WriteFrame(std::vector<uint8_t> frame) {
    // Enqueue and, if no drain is running, drain the queue in order. A single logical writer
    // is ever active, so overlapping SendGameMessage* calls (dispatch + keepalive timer) can
    // never interleave partial frames on the socket.
    write_q_.push_back(std::move(frame));
    if (writing_) co_return;
    writing_ = true;
    while (!write_q_.empty()) {
        auto f = std::move(write_q_.front());
        co_await asio::async_write(sock_, asio::buffer(f), use_awaitable);
        if (!write_q_.empty()) write_q_.pop_front();
    }
    writing_ = false;
}

awaitable<void> GameSession::SendClearGameMessage(uint16_t opcode, std::vector<uint8_t> body) {
    co_await WriteFrame(GamePacketFrame::Encode(opcode, body));
}

awaitable<void> GameSession::SendGameMessage(uint16_t opcode, std::vector<uint8_t> body) {
    co_await WriteFrame(crypt
        ? WorldPacket::EncodeServer(opcode, body, *crypt)
        : GamePacketFrame::Encode(opcode, body));
}

awaitable<void> GameSession::SendGameMessageVia(uint16_t containerOpcode, uint16_t opcode, std::vector<uint8_t> body) {
    co_await WriteFrame(crypt
        ? WorldPacket::EncodeServerVia(containerOpcode, opcode, body, *crypt)
        : GamePacketFrame::Encode(opcode, body));
}

void GameSession::StartKeepalive(uint16_t containerOpcode, uint16_t opcode, std::vector<uint8_t> body, int intervalMs) {
    if (ka_started_) return;
    ka_started_ = true;
    auto self = shared_from_this();
    co_spawn(sock_.get_executor(),
        [self, containerOpcode, opcode, body, intervalMs]() -> awaitable<void> {
            asio::steady_timer t(self->sock_.get_executor());
            for (;;) {
                t.expires_after(std::chrono::milliseconds(intervalMs));
                asio::error_code ec;
                co_await t.async_wait(asio::redirect_error(use_awaitable, ec));
                if (ec || self->keepalive_stop) break;
                try { co_await self->SendGameMessageVia(containerOpcode, opcode, body); }
                catch (const std::exception&) { break; }
            }
        }, detached);
}

awaitable<void> GameSession::Dispatch(uint16_t opcode, std::vector<uint8_t> payload) {
    auto it = server_.handlers_.find(opcode);
    if (it != server_.handlers_.end())
        co_await it->second(*this, payload);
    else if (server_.on_unhandled)
        server_.on_unhandled(*this, opcode, payload);
    co_return;
}

awaitable<void> GameSession::Run() {
    if (server_.on_connected) co_await server_.on_connected(*this);

    std::vector<uint8_t> tmp(8192);
    try {
        for (;;) {
            std::size_t n = co_await sock_.async_read_some(asio::buffer(tmp), use_awaitable);
            if (n == 0) break;
            buf_.insert(buf_.end(), tmp.begin(), tmp.begin() + n);

            for (;;) {
                size_t total = 0;
                if (!GamePacketFrame::TryReadLength(buf_.data(), buf_.size(), total)) break;
                if (total < 4 || buf_.size() < total) break;
                std::vector<uint8_t> frame(buf_.begin(), buf_.begin() + total);
                buf_.erase(buf_.begin(), buf_.begin() + total);

                auto [opcode, payload] = GamePacketFrame::Decode(frame);
                {
                    std::string hex; char hb[4];
                    for (size_t i = 0; i < frame.size() && i < 40; ++i) { std::snprintf(hb, sizeof hb, "%02x ", frame[i]); hex += hb; }
                    std::printf("realm: [RAW IN] outer=0x%04X size=%zu : %s\n", opcode, frame.size(), hex.c_str());
                }
                if (crypt && opcode == WorldPacket::ClientContainer) {
                    try {
                        auto [innerOp, innerBody] = WorldPacket::DecodeContainer(payload, *crypt);
                        std::printf("realm: [RAW IN] container inner=0x%04X (%zuB)\n", innerOp, innerBody.size());
                        co_await Dispatch(innerOp, innerBody);
                    } catch (const std::exception&) {}
                } else {
                    co_await Dispatch(opcode, payload);
                }
            }
        }
    } catch (const std::exception&) {}
    co_return;
}

GameServer::GameServer(asio::io_context& io, const std::string& address, uint16_t port, bool worldChannel)
    : io_(io), acceptor_(io, tcp::endpoint(asio::ip::make_address(address), port)), world_channel_(worldChannel) {}

void GameServer::Start() { co_spawn(io_, AcceptLoop(), detached); }

awaitable<void> GameServer::AcceptLoop() {
    for (;;) {
        tcp::socket sock = co_await acceptor_.async_accept(use_awaitable);
        auto session = std::make_shared<GameSession>(std::move(sock), *this, world_channel_);
        if (world_channel_) session->crypt.emplace(WorldPacket::WorldChannelSeed);
        co_spawn(io_, [session]() -> awaitable<void> { co_await session->Run(); }, detached);
    }
}

} // namespace nexus::net
