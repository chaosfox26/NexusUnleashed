#include "sts/sts_message.h"
#include <algorithm>
#include <cctype>

namespace nexus::sts {

static std::string lower(std::string s) {
    std::transform(s.begin(), s.end(), s.begin(), [](unsigned char c){ return static_cast<char>(std::tolower(c)); });
    return s;
}
static std::string trim(const std::string& s) {
    size_t a = 0, b = s.size();
    while (a < b && (s[a] == ' ' || s[a] == '\t' || s[a] == '\r')) ++a;
    while (b > a && (s[b-1] == ' ' || s[b-1] == '\t' || s[b-1] == '\r')) --b;
    return s.substr(a, b - a);
}

std::optional<std::string> StsRequest::header(const std::string& key) const {
    auto it = headers.find(lower(key));
    if (it == headers.end()) return std::nullopt;
    return it->second;
}
int StsRequest::sequence() const {
    auto s = header("s");
    if (!s) return 0;
    try { return std::stoi(*s); } catch (...) { return 0; }
}

static std::vector<uint8_t> Build(const std::string& statusLine, int sequence,
                                  const std::vector<uint8_t>& body) {
    std::string head;
    head += statusLine; head += "\r\n";
    head += "l:" + std::to_string(body.size()) + "\r\n";
    head += "s:" + std::to_string(sequence) + "R\r\n";
    head += "\r\n";
    std::vector<uint8_t> frame(head.begin(), head.end());
    frame.insert(frame.end(), body.begin(), body.end());
    return frame;
}

std::vector<uint8_t> StsReply::Ok(int sequence, const std::string& xmlBody) {
    return Build(OkStatus, sequence, std::vector<uint8_t>(xmlBody.begin(), xmlBody.end()));
}
std::vector<uint8_t> StsReply::OkRaw(int sequence, const std::vector<uint8_t>& body) {
    return Build(OkStatus, sequence, body);
}
std::vector<uint8_t> StsReply::Error(int sequence, int code, const std::string& xmlBody) {
    return Build(std::string(Version) + " " + std::to_string(code) + " ERROR", sequence,
                 std::vector<uint8_t>(xmlBody.begin(), xmlBody.end()));
}

void StsParser::Feed(const uint8_t* data, size_t len) {
    buffer_.insert(buffer_.end(), data, data + len);
}

static int IndexOfDoubleNewline(const std::vector<uint8_t>& buf, size_t& body_start) {
    for (size_t i = 0; i + 1 < buf.size(); ++i) {
        if (buf[i] == '\n') {
            if (buf[i + 1] == '\n') { body_start = i + 2; return static_cast<int>(i + 1); }
            if (i + 2 < buf.size() && buf[i + 1] == '\r' && buf[i + 2] == '\n') {
                body_start = i + 3; return static_cast<int>(i + 1);
            }
        }
    }
    return -1;
}

std::optional<StsRequest> StsParser::TryReadRequest() {
    size_t body_start = 0;
    int head_end = IndexOfDoubleNewline(buffer_, body_start);
    if (head_end < 0) return std::nullopt;

    std::string head(buffer_.begin(), buffer_.begin() + head_end);
    StsRequest req;

    std::vector<std::string> lines;
    size_t pos = 0;
    while (pos <= head.size()) {
        size_t nl = head.find('\n', pos);
        if (nl == std::string::npos) { lines.push_back(head.substr(pos)); break; }
        lines.push_back(head.substr(pos, nl - pos));
        pos = nl + 1;
    }

    if (!lines.empty()) {
        std::string rl = trim(lines[0]);
        size_t sp1 = rl.find(' ');
        if (sp1 != std::string::npos) {
            req.method = rl.substr(0, sp1);
            size_t sp2 = rl.find(' ', sp1 + 1);
            req.uri = (sp2 == std::string::npos) ? rl.substr(sp1 + 1) : rl.substr(sp1 + 1, sp2 - sp1 - 1);
        }
    }
    for (size_t i = 1; i < lines.size(); ++i) {
        std::string line = trim(lines[i]);
        size_t colon = line.find(':');
        if (colon != std::string::npos && colon > 0)
            req.headers[lower(line.substr(0, colon))] = trim(line.substr(colon + 1));
    }

    int body_len = 0;
    if (auto l = req.header("l")) { try { body_len = std::stoi(*l); } catch (...) { body_len = 0; } }
    if (buffer_.size() < body_start + static_cast<size_t>(body_len)) return std::nullopt;

    req.body.assign(buffer_.begin() + body_start, buffer_.begin() + body_start + body_len);
    buffer_.erase(buffer_.begin(), buffer_.begin() + body_start + body_len);
    return req;
}

} // namespace nexus::sts
