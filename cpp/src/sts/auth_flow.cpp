#include "sts/auth_flow.h"
#include "crypto/sts_srp.h"
#include <openssl/rand.h>
#include <atomic>
#include <mutex>
#include <unordered_map>
#include <string>

namespace nexus::sts {

using Bytes = std::vector<uint8_t>;

namespace {
    std::mutex g_mtx;
    std::unordered_map<std::string, long> g_token_to_account;
    std::atomic<long> g_last_account{0};
}
void AuthSession::Register(const std::string& tokenHex, long accountId) {
    { std::lock_guard<std::mutex> lk(g_mtx); if (!tokenHex.empty()) g_token_to_account[tokenHex] = accountId; }
    g_last_account = accountId;
}
long AuthSession::ResolveToken(const std::string& tokenHex) {
    std::lock_guard<std::mutex> lk(g_mtx);
    auto it = g_token_to_account.find(tokenHex);
    return it == g_token_to_account.end() ? 0 : it->second;
}
long AuthSession::LastAccountId() { return g_last_account.load(); }

static const char* B64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

static std::string base64_encode(const Bytes& in) {
    std::string out;
    size_t i = 0;
    while (i + 3 <= in.size()) {
        uint32_t n = (in[i] << 16) | (in[i+1] << 8) | in[i+2];
        out += B64[(n >> 18) & 63]; out += B64[(n >> 12) & 63];
        out += B64[(n >> 6) & 63];  out += B64[n & 63];
        i += 3;
    }
    size_t rem = in.size() - i;
    if (rem == 1) {
        uint32_t n = in[i] << 16;
        out += B64[(n >> 18) & 63]; out += B64[(n >> 12) & 63]; out += "==";
    } else if (rem == 2) {
        uint32_t n = (in[i] << 16) | (in[i+1] << 8);
        out += B64[(n >> 18) & 63]; out += B64[(n >> 12) & 63]; out += B64[(n >> 6) & 63]; out += '=';
    }
    return out;
}
static int b64val(char c) {
    if (c >= 'A' && c <= 'Z') return c - 'A';
    if (c >= 'a' && c <= 'z') return c - 'a' + 26;
    if (c >= '0' && c <= '9') return c - '0' + 52;
    if (c == '+') return 62;
    if (c == '/') return 63;
    return -1;
}
static Bytes base64_decode(const std::string& s) {
    Bytes out; int buf = 0, bits = 0;
    for (char c : s) {
        if (c == '=' || c == '\r' || c == '\n' || c == ' ') continue;
        int v = b64val(c); if (v < 0) continue;
        buf = (buf << 6) | v; bits += 6;
        if (bits >= 8) { bits -= 8; out.push_back(static_cast<uint8_t>((buf >> bits) & 0xFF)); }
    }
    return out;
}

static std::string xml_escape(const std::string& s) {
    std::string o; o.reserve(s.size());
    for (char c : s) switch (c) {
        case '&': o += "&amp;"; break;  case '<': o += "&lt;"; break;
        case '>': o += "&gt;"; break;   case '"': o += "&quot;"; break;
        case '\'': o += "&apos;"; break; default: o += c;
    }
    return o;
}
static std::string xml_field(const std::string& xml, const std::string& tag) {
    std::string open = "<" + tag + ">", close = "</" + tag + ">";
    size_t i = xml.find(open); if (i == std::string::npos) return "";
    i += open.size();
    size_t j = xml.find(close, i);
    return j == std::string::npos ? "" : xml.substr(i, j - i);
}

static Bytes blob_pack(std::initializer_list<Bytes> parts) {
    Bytes buf;
    for (const auto& p : parts) {
        uint32_t n = static_cast<uint32_t>(p.size());
        buf.push_back(uint8_t(n)); buf.push_back(uint8_t(n >> 8));
        buf.push_back(uint8_t(n >> 16)); buf.push_back(uint8_t(n >> 24));
        buf.insert(buf.end(), p.begin(), p.end());
    }
    return buf;
}
static std::vector<Bytes> blob_unpack(const Bytes& blob) {
    std::vector<Bytes> list; size_t o = 0;
    while (o + 4 <= blob.size()) {
        uint32_t len = blob[o] | (blob[o+1] << 8) | (blob[o+2] << 16) | (uint32_t(blob[o+3]) << 24);
        o += 4;
        if (o + len > blob.size()) break;
        list.emplace_back(blob.begin() + o, blob.begin() + o + len);
        o += len;
    }
    return list;
}

static std::string new_token_hex() {
    uint8_t b[16];
    RAND_bytes(b, 16);
    static const char* h = "0123456789abcdef";
    std::string s; s.reserve(32);
    for (int i = 0; i < 16; ++i) { s += h[b[i] >> 4]; s += h[b[i] & 0xF]; }
    return s;
}

static std::string key_data_body(const Bytes& blob) {
    return "<Reply>\n<KeyData>" + base64_encode(blob) + "</KeyData>\n</Reply>\n";
}

void AuthFlow::Register(StsServer& server, IAccountStore& accounts) {
    server.On("/Sts/Connect", [](StsSessionState&, const StsRequest& r) -> HandlerResult {
        return { StsReply::Ok(r.sequence(), ""), std::nullopt };
    });
    server.On("/Sts/Ping", [](StsSessionState&, const StsRequest& r) -> HandlerResult {
        return { StsReply::Ok(r.sequence(), ""), std::nullopt };
    });

    server.On("/Auth/LoginStart", [&accounts](StsSessionState& st, const StsRequest& r) -> HandlerResult {
        st.login = xml_field(r.body_text(), "LoginName");
        auto creds = accounts.GetSrpCredentials(st.login);
        if (!creds || creds->second.empty())
            return { StsReply::Error(r.sequence(), 403), std::nullopt };
        st.srp = std::make_shared<crypto::StsSrp>(creds->first, creds->second, st.login);
        Bytes B = st.srp->StartHandshake();
        std::printf("[STS-SRP] LoginStart: game-SRP (little-endian)\n");
        Bytes blob = blob_pack({ creds->first, B });
        return { StsReply::Ok(r.sequence(), key_data_body(blob)), std::nullopt };
    });

    server.On("/Auth/KeyData", [](StsSessionState& st, const StsRequest& r) -> HandlerResult {
        if (!st.srp)
            return { StsReply::Error(r.sequence(), 409), std::nullopt };
        Bytes clientBlob = base64_decode(xml_field(r.body_text(), "KeyData"));
        auto parts = blob_unpack(clientBlob);
        Bytes a  = parts.size() > 0 ? parts[0] : Bytes{};
        Bytes m1 = parts.size() > 1 ? parts[1] : Bytes{};
        Bytes m2, K;
        if (!st.srp->Verify(a, m1, m2, K)) {
            std::printf("[STS-SRP] KeyData proof did NOT verify (A=%zuB)\n", a.size());
            return { StsReply::Error(r.sequence(), 403), std::nullopt };
        }
        std::printf("[STS-SRP] proof VERIFIED (game-SRP little-endian)\n");
        st.authed = true;
        st.session_key = K;
        Bytes blob = blob_pack({ m2 });
        return { StsReply::Ok(r.sequence(), key_data_body(blob)), K };
    });

    server.On("/Auth/LoginFinish", [&accounts](StsSessionState& st, const StsRequest& r) -> HandlerResult {
        long uid = accounts.GetUserId(st.login);
        std::string e = xml_escape(st.login);
        std::string body =
            "<Reply>\n<AuthType>Password</AuthType>\n<UserId>" + std::to_string(uid) +
            "</UserId>\n<UserCenter>1</UserCenter>\n<UserName>" + e +
            "</UserName>\n<LocationId>1</LocationId>\n<AccessMask>4294967295</AccessMask>\n"
            "<Status>0</Status>\n<Roles>1</Roles>\n</Reply>\n";
        return { StsReply::Ok(r.sequence(), body), std::nullopt };
    });

    server.On("/GameAccount/ListMyAccounts", [&accounts](StsSessionState& st, const StsRequest& r) -> HandlerResult {
        long uid = accounts.GetUserId(st.login);
        std::string e = xml_escape(st.login);
        std::string alias = st.login.find('@') != std::string::npos ? st.login.substr(0, st.login.find('@')) : st.login;
        std::string ea = xml_escape(alias);
        std::string u = std::to_string(uid);
        std::string body =
            "<Reply>\n<GameAccount>\n<GameAccountId>" + u + "</GameAccountId>\n<AccountId>" + u +
            "</AccountId>\n<LoginName>" + e + "</LoginName>\n<UserId>" + u + "</UserId>\n<UserName>" + e +
            "</UserName>\n<Email>" + e + "</Email>\n<Alias>" + ea + "</Alias>\n<AccountAlias>" + ea +
            "</AccountAlias>\n<GameCode>wildstar</GameCode>\n<AppId>0</AppId>\n<UserCenter>1</UserCenter>\n"
            "<State>1</State>\n<Status>0</Status>\n<Roles>1</Roles>\n</GameAccount>\n</Reply>\n";
        return { StsReply::Ok(r.sequence(), body), std::nullopt };
    });

    server.On("/Auth/RequestGameToken", [&accounts](StsSessionState& st, const StsRequest& r) -> HandlerResult {
        if (st.authed && !st.login.empty()) {
            std::string token = new_token_hex();
            accounts.StoreGameToken(st.login, token);
            long uid = accounts.GetUserId(st.login);
            AuthSession::Register(token, uid);
            std::string body = "<Reply>\n<Token>" + token + "</Token>\n</Reply>\n";
            return { StsReply::Ok(r.sequence(), body), std::nullopt };
        }
        return { StsReply::Error(r.sequence(), 401), std::nullopt };
    });
}

} // namespace nexus::sts
