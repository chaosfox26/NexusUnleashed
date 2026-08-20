// NexusUnleashed - clean-room authored. See db_store.h. 1:1 with the C# DB stores.
#include "db/db_store.h"
#include "proto/game_data.h"
#include <mysql/mysql.h>
#include <stdexcept>
#include <cstring>
#include <algorithm>
#include <cctype>

namespace nexus::db {

using Bytes = std::vector<uint8_t>;

static std::string lower(std::string s) {
    std::transform(s.begin(), s.end(), s.begin(), [](unsigned char c){ return (char)std::tolower(c); });
    return s;
}

ConnInfo ParseConnString(const std::string& s) {
    ConnInfo ci;
    size_t pos = 0;
    while (pos < s.size()) {
        size_t sc = s.find(';', pos);
        std::string part = s.substr(pos, sc == std::string::npos ? std::string::npos : sc - pos);
        size_t eq = part.find('=');
        if (eq != std::string::npos) {
            std::string k = lower(part.substr(0, eq));
            // trim spaces from key
            k.erase(std::remove_if(k.begin(), k.end(), [](unsigned char c){ return std::isspace(c); }), k.end());
            std::string v = part.substr(eq + 1);
            if (k == "server" || k == "host" || k == "data source") ci.host = v;
            else if (k == "port") { try { ci.port = (unsigned)std::stoul(v); } catch (...) {} }
            else if (k == "user" || k == "uid" || k == "user id") ci.user = v;
            else if (k == "password" || k == "pwd") ci.pass = v;
            else if (k == "database" || k == "initial catalog") ci.database = v;
        }
        if (sc == std::string::npos) break;
        pos = sc + 1;
    }
    return ci;
}

static Bytes from_hex(const std::string& hex) {
    Bytes b; b.reserve(hex.size() / 2);
    auto nib = [](char c) -> int {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        return 0;
    };
    for (size_t i = 0; i + 1 < hex.size(); i += 2)
        b.push_back((uint8_t)((nib(hex[i]) << 4) | nib(hex[i + 1])));
    return b;
}

// RAII connection.
namespace {
struct Conn {
    MYSQL* m = nullptr;
    Conn(const ConnInfo& ci, const std::string& dbName) {
        m = mysql_init(nullptr);
        if (!m) throw std::runtime_error("mysql_init failed");
        if (!mysql_real_connect(m, ci.host.c_str(), ci.user.c_str(), ci.pass.c_str(),
                                dbName.c_str(), ci.port, nullptr, 0)) {
            std::string e = mysql_error(m);
            mysql_close(m); m = nullptr;
            throw std::runtime_error("mysql connect failed: " + e);
        }
    }
    ~Conn() { if (m) mysql_close(m); }
    std::string escape(const std::string& s) const {
        std::string out(s.size() * 2 + 1, '\0');
        unsigned long n = mysql_real_escape_string(m, out.data(), s.c_str(), (unsigned long)s.size());
        out.resize(n);
        return out;
    }
};
} // namespace

DbAccountStore::DbAccountStore(const std::string& connString) : ci_(ParseConnString(connString)) {}

std::optional<std::pair<Bytes, Bytes>> DbAccountStore::GetSrpCredentials(const std::string& login) {
    if (login.empty()) return std::nullopt;
    Conn c(ci_, ci_.database);
    std::string q = "SELECT s, v FROM account WHERE email='" + c.escape(login) + "' LIMIT 1";
    if (mysql_real_query(c.m, q.c_str(), (unsigned long)q.size()) != 0) return std::nullopt;
    MYSQL_RES* res = mysql_store_result(c.m);
    if (!res) return std::nullopt;
    std::optional<std::pair<Bytes, Bytes>> out;
    if (MYSQL_ROW row = mysql_fetch_row(res)) {
        if (row[0] && row[1]) out = std::make_pair(from_hex(row[0]), from_hex(row[1]));
    }
    mysql_free_result(res);
    return out;
}

long DbAccountStore::GetUserId(const std::string& login) {
    if (login.empty()) return 0;
    Conn c(ci_, ci_.database);
    std::string q = "SELECT id FROM account WHERE email='" + c.escape(login) + "' LIMIT 1";
    if (mysql_real_query(c.m, q.c_str(), (unsigned long)q.size()) != 0) return 0;
    MYSQL_RES* res = mysql_store_result(c.m);
    if (!res) return 0;
    long id = 0;
    if (MYSQL_ROW row = mysql_fetch_row(res)) if (row[0]) id = std::stol(row[0]);
    mysql_free_result(res);
    return id;
}

void DbAccountStore::StoreGameToken(const std::string& login, const std::string& tokenHex) {
    Conn c(ci_, ci_.database);
    std::string q = "UPDATE account SET gameToken='" + c.escape(tokenHex) +
                    "' WHERE email='" + c.escape(login) + "'";
    mysql_real_query(c.m, q.c_str(), (unsigned long)q.size());
}

DbCharacterStore::DbCharacterStore(const std::string& authConnString) : ci_(ParseConnString(authConnString)) {
    ci_.database = "characterdb";   // same server, swap db (matches DbCharacterStore.cs)
}

std::vector<proto::CharacterRecord> DbCharacterStore::GetCharacters(long accountId) {
    std::vector<proto::CharacterRecord> list;
    if (accountId <= 0) return list;
    Conn c(ci_, ci_.database);
    std::string q =
        "SELECT id, name, sex, race, class, level, factionId, "
        "locationX, locationY, locationZ, worldId FROM `character` "
        "WHERE accountId=" + std::to_string(accountId) + " AND deleteTime IS NULL ORDER BY id";
    if (mysql_real_query(c.m, q.c_str(), (unsigned long)q.size()) != 0) return list;
    MYSQL_RES* res = mysql_store_result(c.m);
    if (!res) return list;
    while (MYSQL_ROW row = mysql_fetch_row(res)) {
        proto::CharacterRecord r;
        r.Id        = row[0] ? std::stoull(row[0]) : 0;
        r.Name      = row[1] ? row[1] : "";
        r.Sex       = row[2] ? (uint32_t)std::stoul(row[2]) : 0;
        r.Race      = row[3] ? (uint32_t)std::stoul(row[3]) : 0;
        r.Class     = row[4] ? (uint32_t)std::stoul(row[4]) : 0;
        r.Level     = row[5] ? (uint32_t)std::stoul(row[5]) : 0;
        r.FactionId = row[6] ? (uint32_t)std::stoul(row[6]) : 0;
        r.LocationX = row[7] ? std::stof(row[7]) : 0.f;
        r.LocationY = row[8] ? std::stof(row[8]) : 0.f;
        r.LocationZ = row[9] ? std::stof(row[9]) : 0.f;
        r.WorldId   = row[10] ? (uint32_t)std::stoul(row[10]) : 0;
        list.push_back(std::move(r));
    }
    mysql_free_result(res);

    // Attach each character's stored visuals (character_appearance: slot -> displayId). This is
    // what dresses the char-select model; without it the client renders a black silhouette.
    for (auto& r : list) {
        std::string aq = "SELECT slot, displayId FROM character_appearance WHERE id="
                         + std::to_string(r.Id) + " ORDER BY slot";
        if (mysql_real_query(c.m, aq.c_str(), (unsigned long)aq.size()) != 0) continue;
        MYSQL_RES* ares = mysql_store_result(c.m);
        if (!ares) continue;
        while (MYSQL_ROW arow = mysql_fetch_row(ares)) {
            if (arow[0] && arow[1])
                r.Appearance.emplace_back((uint32_t)std::stoul(arow[0]), (uint32_t)std::stoul(arow[1]));
        }
        mysql_free_result(ares);
    }
    return list;
}

uint64_t DbCharacterStore::CreateCharacter(long accountId, const NewCharacter& nc) {
    if (accountId <= 0) return 0;
    Conn c(ci_, ci_.database);

    // Assign the next id (character.id is a manual PK, no AUTO_INCREMENT).
    uint64_t newId = 1;
    {
        const char* mq = "SELECT COALESCE(MAX(id),0)+1 FROM `character`";
        if (mysql_real_query(c.m, mq, (unsigned long)std::strlen(mq)) == 0) {
            if (MYSQL_RES* r = mysql_store_result(c.m)) {
                if (MYSQL_ROW row = mysql_fetch_row(r)) if (row[0]) newId = std::stoull(row[0]);
                mysql_free_result(r);
            }
        }
    }

    auto f = [](float v){ return std::to_string(v); };
    std::string q =
        "INSERT INTO `character` "
        "(id, accountId, name, sex, race, class, level, factionId, activePath, "
        " worldId, worldZoneId, locationX, locationY, locationZ) VALUES ("
        + std::to_string(newId) + ", "
        + std::to_string(accountId) + ", '"
        + c.escape(nc.Name) + "', "
        + std::to_string(nc.Sex) + ", "
        + std::to_string(nc.Race) + ", "
        + std::to_string(nc.Class) + ", 1, "
        + std::to_string(nc.FactionId) + ", "
        + std::to_string(nc.ActivePath) + ", "
        + std::to_string(nc.WorldId) + ", "
        + std::to_string(nc.WorldZoneId) + ", "
        + f(nc.LocationX) + ", " + f(nc.LocationY) + ", " + f(nc.LocationZ) + ")";

    if (mysql_real_query(c.m, q.c_str(), (unsigned long)q.size()) != 0) return 0;

    // Persist the chosen sliders + their resolved visuals so the character renders on reconnect.
    if (!nc.Customization.empty()) {
        // 1) raw sliders -> character_customisation (id, label, value)
        std::string cq = "INSERT INTO character_customisation (id, label, value) VALUES ";
        std::vector<proto::CustomizationChoice> choices;
        bool first = true;
        for (const auto& p : nc.Customization) {
            if (!first) cq += ",";
            cq += "(" + std::to_string(newId) + "," + std::to_string(p.first) + "," + std::to_string(p.second) + ")";
            first = false;
            choices.push_back({p.first, p.second});
        }
        mysql_real_query(c.m, cq.c_str(), (unsigned long)cq.size());

        // 2) resolve via the client's own CharacterCustomization table -> character_appearance
        auto visuals = proto::GameData::ResolveAppearance(nc.Race, nc.Sex, choices);
        if (!visuals.empty()) {
            std::string aq = "INSERT INTO character_appearance (id, slot, displayId) VALUES ";
            first = true;
            for (const auto& v : visuals) {
                if (!first) aq += ",";
                aq += "(" + std::to_string(newId) + "," + std::to_string(v.Slot) + "," + std::to_string(v.DisplayId) + ")";
                first = false;
            }
            mysql_real_query(c.m, aq.c_str(), (unsigned long)aq.size());
        }
    }
    return newId;
}

} // namespace nexus::db
