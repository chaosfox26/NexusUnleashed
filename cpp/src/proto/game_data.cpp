#include "proto/game_data.h"
#include <cstdio>
#include <cstdlib>
#include <fstream>
#include <sstream>

namespace nexus::proto {

std::map<uint32_t, CharacterCreationRow> GameData::creation_;
std::vector<CharacterCustomizationRow> GameData::customization_;

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
        if (header) { header = false; continue; }
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

static uint32_t U32(const std::string& s) { return (uint32_t)std::strtoul(s.c_str(), nullptr, 10); }

size_t GameData::LoadCharacterCustomization(const std::string& tsvPath) {
    std::ifstream f(tsvPath);
    if (!f) return 0;
    std::string line;
    bool header = true;
    customization_.clear();
    while (std::getline(f, line)) {
        if (header) { header = false; continue; }
        if (line.empty()) continue;
        std::stringstream ss(line);
        std::string c[8];
        for (int i = 0; i < 8; ++i) std::getline(ss, c[i], '\t');
        CharacterCustomizationRow r;
        r.RaceId = U32(c[0]); r.Gender = U32(c[1]); r.SlotId = U32(c[2]); r.DisplayId = U32(c[3]);
        r.Label00 = U32(c[4]); r.Value00 = U32(c[5]); r.Label01 = U32(c[6]); r.Value01 = U32(c[7]);
        customization_.push_back(r);
    }
    return customization_.size();
}

std::vector<AppearanceVisual> GameData::ResolveAppearance(
    uint32_t race, uint32_t gender, const std::vector<CustomizationChoice>& choices) {
    std::map<uint32_t, uint32_t> chosen;
    for (const auto& c : choices) chosen[c.Label] = c.Value;

    std::map<uint32_t, uint32_t> bySlot;
    for (const auto& r : customization_) {
        if (r.RaceId != race || r.Gender != gender) continue;
        if (r.Label00 != 0) { auto it = chosen.find(r.Label00); if (it == chosen.end() || it->second != r.Value00) continue; }
        if (r.Label01 != 0) { auto it = chosen.find(r.Label01); if (it == chosen.end() || it->second != r.Value01) continue; }
        bySlot[r.SlotId] = r.DisplayId;
    }
    std::vector<AppearanceVisual> out;
    out.reserve(bySlot.size());
    for (const auto& kv : bySlot) out.push_back({kv.first, kv.second});
    return out;
}

} // namespace nexus::proto
