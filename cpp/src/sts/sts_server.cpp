// NexusUnleashed - clean-room authored. See sts_server.h. 1:1 with StsServer.cs/StsSession.
#include "sts/sts_server.h"
#include "crypto/arc4.h"
#include <cstdio>

using asio::ip::tcp;
using asio::awaitable;
using asio::co_spawn;
using asio::detached;
using asio::use_awaitable;

namespace nexus::sts {

StsServer::StsServer(asio::io_context& io, const std::string& address, uint16_t port)
    : io_(io), acceptor_(io, tcp::endpoint(asio::ip::make_address(address), port)) {}

void StsServer::Start() {
    co_spawn(io_, AcceptLoop(), detached);
}

awaitable<void> StsServer::AcceptLoop() {
    for (;;) {
        tcp::socket sock = co_await acceptor_.async_accept(use_awaitable);
        co_spawn(io_, Session(std::move(sock)), detached);
    }
}

HandlerResult StsServer::Dispatch(StsSessionState& st, const StsRequest& req) {
    if (request_observer) { try { request_observer(req); } catch (...) {} }
    auto it = routes_.find(req.uri);
    if (it == routes_.end())
        return { StsReply::Error(req.sequence(), 400), std::nullopt };   // unknown message
    try {
        return it->second(st, req);
    } catch (const std::exception& ex) {
        std::printf("[ERROR] STS handler %s threw: %s\n", req.uri.c_str(), ex.what());
        return { StsReply::Error(req.sequence(), 500), std::nullopt };
    }
}

awaitable<void> StsServer::Session(tcp::socket sock) {
    StsSessionState st;
    StsParser parser;
    crypto::Arc4 rx, tx;
    bool enc = false;
    std::vector<uint8_t> buf(8192);
    try {
        for (;;) {
            std::size_t n = co_await sock.async_read_some(asio::buffer(buf), use_awaitable);
            if (n == 0) break;
            if (enc) rx.ProcessBuffer(buf.data(), n);   // decrypt inbound after SRP
            parser.Feed(buf.data(), n);

            while (auto req = parser.TryReadRequest()) {
                HandlerResult res = Dispatch(st, *req);
                std::vector<uint8_t> frame = std::move(res.reply);
                if (enc) tx.ProcessBuffer(frame);        // encrypt the reply after SRP
                co_await asio::async_write(sock, asio::buffer(frame), use_awaitable);
                // Enable the ARC4 streams AFTER sending the (plaintext) M2 reply.
                if (res.enable_encryption) {
                    rx.PrepareKey(*res.enable_encryption);
                    tx.PrepareKey(*res.enable_encryption);
                    enc = true;
                }
            }
        }
    } catch (const std::exception&) {
        // connection closed / reset — never fatal to the server
    }
    co_return;
}

} // namespace nexus::sts
