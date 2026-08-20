// NexusUnleashed - clean-room authored. Loads the client's own data tables (facts shipped in
// the 16042 client, uncopyrightable, zero NexusForever) that the engine is driven from. First
// table: CharacterCreation — one ID expands to race/class/sex/faction/start/starting-items,
// exactly as the character-creation window resolves it. Exported to TSV via tbl_reader.
#pragma once
#include <cstdint>
#include <map>
#include <string>
#include <vector>

namespace nexus::proto {

struct CharacterCreationRow {
    uint32_t Id = 0, ClassId = 0, RaceId = 0, Sex = 0, FactionId = 0, StartEnum = 0;
    std::vector<uint32_t> Items;   // starting outfit/gear item ids
};

class GameData {
public:
    // Load data/character-creation.tsv (id,classId,raceId,sex,factionId,startEnum,items).
    // Safe to call once at boot; returns rows loaded.
    static size_t LoadCharacterCreation(const std::string& tsvPath);
    // Look up a CharacterCreation ID; nullptr if unknown.
    static const CharacterCreationRow* Creation(uint32_t id);

private:
    static std::map<uint32_t, CharacterCreationRow> creation_;
};

} // namespace nexus::proto
