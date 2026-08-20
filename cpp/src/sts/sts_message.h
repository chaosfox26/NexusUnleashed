// NexusUnleashed - clean-room authored. C++ port of StsMessage.cs — the STS text
// protocol (HTTP-shaped): request line "POST /Service/Message STS/1.0", headers
// "l:<bodylen>"/"s:<seq>", blank line, XML body. Replies: "STS/1.0 200  OK" (TWO
// spaces), "l:", "s:<seq>R". Provenance: measured from the client's StsConnLib.
#pragma once
#include <cstdint>
#include <string>
#include <vector>
#include <map>
#include <optional>

namespace nexus::sts {

/// A parsed STS request (client -> server).
struct StsRequest {
    std::string method = "POST";
    std::string uri;                                  // "/Auth/LoginStart" — routing key
    std::map<std::string, std::string> headers;       // case-insensitive keys stored lowercased
    std::vector<uint8_t> body;

    int sequence() const;
    std::string body_text() const { return std::string(body.begin(), body.end()); }
    std::optional<std::string> header(const std::string& key) const;
};

/// Builds an STS reply (server -> client).
class StsReply {
public:
    static constexpr const char* Version  = "STS/1.0";
    // "200" then TWO spaces then "OK" — the client parses this exact form.
    static constexpr const char* OkStatus = "STS/1.0 200  OK";

    static std::vector<uint8_t> Ok(int sequence, const std::string& xmlBody);
    static std::vector<uint8_t> OkRaw(int sequence, const std::vector<uint8_t>& body);
    static std::vector<uint8_t> Error(int sequence, int code, const std::string& xmlBody = "");
};

/// Incremental parser: feed bytes, pull complete requests (null until a full frame).
class StsParser {
public:
    void Feed(const uint8_t* data, size_t len);
    void Feed(const std::vector<uint8_t>& v) { Feed(v.data(), v.size()); }
    std::optional<StsRequest> TryReadRequest();

private:
    std::vector<uint8_t> buffer_;
};

} // namespace nexus::sts
