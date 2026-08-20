// NexusUnleashed - clean-room authored. See game_data.h.
#include "proto/game_data.h"
#include <cstdio>
#include <cstdlib>
#include <fstream>
#include <sstream>

namespace nexus::proto {

std::map<uint32_t, CharacterCreationRow> GameData::creation_;

static std::vector<uint32_t> ParseCsvU32(const std::string& s) {
    std::vector<uint32_t> out;
    std::stringstream ss(s);
    std::string tok;
    while (std::getline(ss, tok, ',')) {
        if (!tok.empty()) out.push_back((uint32_t)std::strtoul(tok.c_str(), nullptr, 10));
    }
    return out;
}

size_t GameData::LoadCharacterCreation(const std::string& tsvPath) {
    std::ifstream f(tsvPath);
    if (!f) return 0;
    std::string line;
    bool header = true;
    creation_.clear();
    while (std::getline(f, line)) {
        if (header) { header = false; continue; }   // skip column header
        if (line.empty()) continue;
        std::stringstream ss(line);
        std::string id, cls, race, sex, fac, start, items;
        std::getline(ss, id, '\t'); std::getline(ss, cls, '\t'); std::getline(ss, race, '\t');
        std::getline(ss, sex, '\t'); std::getline(ss, fac, '\t'); std::getline(ss, start, '\t');
        std::getline(ss, items, '\t');
        CharacterCreationRow r;
        r.Id        = (uint32_t)std::strtoul(id.c_str(), nullptr, 10);
        r.ClassId   = (uint32_t)std::strtoul(cls.c_str(), nullptr, 10);
        r.RaceId    = (uint32_t)std::strtoul(race.c_str(), nullptr, 10);
        r.Sex       = (uint32_t)std::strtoul(sex.c_str(), nullptr, 10);
        r.FactionId = (uint32_t)std::strtoul(fac.c_str(), nullptr, 10);
        r.StartEnum = (uint32_t)std::strtoul(start.c_str(), nullptr, 10);
        r.Items     = ParseCsvU32(items);
        creation_[r.Id] = std::move(r);
    }
    return creation_.size();
}

const CharacterCreationRow* GameData::Creation(uint32_t id) {
    auto it = creation_.find(id);
    return it == creation_.end() ? nullptr : &it->second;
}

} // namespace nexus::proto
