#pragma once
#include <cstdint>
#include <functional>
#include <map>
#include <memory>
#include <optional>
#include <string>
#include <vector>
#include <asio.hpp>
#include "sts/sts_message.h"
#include "crypto/sts_srp.h"

namespace nexus::sts {

struct StsSessionState {
    std::string login;
    std::shared_ptr<crypto::StsSrp> srp;
    bool authed = false;
    std::vector<uint8_t> session_key;
};

struct HandlerResult {
    std::vector<uint8_t> reply;
    std::optional<std::vector<uint8_t>> enable_encryption;
};

using StsHandler = std::function<HandlerResult(StsSessionState&, const StsRequest&)>;

class StsServer {
public:
    StsServer(asio::io_context& io, const std::string& address, uint16_t port);

    void On(std::string uri, StsHandler handler) { routes_[std::move(uri)] = std::move(handler); }
    std::function<void(const StsRequest&)> request_observer;

    void Start();

private:
    asio::awaitable<void> AcceptLoop();
    asio::awaitable<void> Session(asio::ip::tcp::socket sock);
    HandlerResult Dispatch(StsSessionState& st, const StsRequest& req);

    asio::io_context& io_;
    asio::ip::tcp::acceptor acceptor_;
    std::map<std::string, StsHandler> routes_;
};

} // namespace nexus::sts
