#pragma once
#include <cstdint>
#include <string>
#include <vector>
#include <map>
#include <optional>

namespace nexus::sts {

struct StsRequest {
    std::string method = "POST";
    std::string uri;
    std::map<std::string, std::string> headers;
    std::vector<uint8_t> body;

    int sequence() const;
    std::string body_text() const { return std::string(body.begin(), body.end()); }
    std::optional<std::string> header(const std::string& key) const;
};

class StsReply {
public:
    static constexpr const char* Version  = "STS/1.0";
    static constexpr const char* OkStatus = "STS/1.0 200  OK";

    static std::vector<uint8_t> Ok(int sequence, const std::string& xmlBody);
    static std::vector<uint8_t> OkRaw(int sequence, const std::vector<uint8_t>& body);
    static std::vector<uint8_t> Error(int sequence, int code, const std::string& xmlBody = "");
};

class StsParser {
public:
    void Feed(const uint8_t* data, size_t len);
    void Feed(const std::vector<uint8_t>& v) { Feed(v.data(), v.size()); }
    std::optional<StsRequest> TryReadRequest();

private:
    std::vector<uint8_t> buffer_;
};

} // namespace nexus::sts
