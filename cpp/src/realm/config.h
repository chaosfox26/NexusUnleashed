// NexusUnleashed - clean-room authored. C++ port of RealmConfig.cs — realm.json loader.
#pragma once
#include <cstdint>
#include <fstream>
#include <string>
#include <nlohmann/json.hpp>

namespace nexus::realm {

struct RealmConfig {
    std::string realm_name = "NexusUnleashed";
    std::string motd;
    std::string bind_address = "0.0.0.0";
    uint16_t sts_port = 6600;
    uint16_t auth_port = 23115;
    uint16_t world_port = 24000;
    std::string auth_database;   // Server=..;Port=..;User=..;Password=..;Database=authdb

    static RealmConfig Load(const std::string& path) {
        RealmConfig c;
        std::ifstream f(path);
        if (!f) return c;
        nlohmann::json j; f >> j;
        auto get = [&](const char* k, auto& dst) { if (j.contains(k) && !j[k].is_null()) dst = j[k].get<std::decay_t<decltype(dst)>>(); };
        get("RealmName", c.realm_name);
        get("MessageOfTheDay", c.motd);
        get("BindAddress", c.bind_address);
        get("StsPort", c.sts_port);
        get("AuthPort", c.auth_port);
        get("WorldPort", c.world_port);
        get("AuthDatabase", c.auth_database);
        return c;
    }
};

} // namespace nexus::realm
