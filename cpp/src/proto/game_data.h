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

// One CharacterCustomization.tbl row: for a (race,gender), the pair(s) (label,value) that must
// be chosen for this visual to apply, and the (slot,displayId) it produces. label00==0 means a
// base/default row (always applies); label01!=0 means a two-condition combo row.
struct CharacterCustomizationRow {
    uint32_t RaceId = 0, Gender = 0, SlotId = 0, DisplayId = 0;
    uint32_t Label00 = 0, Value00 = 0, Label01 = 0, Value01 = 0;
};

// A resolved visual: itemSlot -> itemDisplayId (character_appearance row).
struct AppearanceVisual { uint32_t Slot = 0; uint32_t DisplayId = 0; };
// A chosen customization slider (character_customisation row).
struct CustomizationChoice { uint32_t Label = 0; uint32_t Value = 0; };

class GameData {
public:
    // Load data/character-creation.tsv (id,classId,raceId,sex,factionId,startEnum,items).
    // Safe to call once at boot; returns rows loaded.
    static size_t LoadCharacterCreation(const std::string& tsvPath);
    // Look up a CharacterCreation ID; nullptr if unknown.
    static const CharacterCreationRow* Creation(uint32_t id);

    // Load data/character-customization.tsv (raceId,gender,itemSlotId,itemDisplayId,l00,v00,l01,v01).
    static size_t LoadCharacterCustomization(const std::string& tsvPath);
    // Resolve a character's chosen sliders into the equipped visuals the client renders.
    // Rule (validated against a real stored character): a row for (race,gender) applies when
    // its label00 (if !=0) matches the chosen value AND its label01 (if !=0) matches too;
    // label00==0 rows are always-on base visuals. Deduped by slot (last row wins, table order).
    static std::vector<AppearanceVisual> ResolveAppearance(
        uint32_t race, uint32_t gender, const std::vector<CustomizationChoice>& choices);

private:
    static std::map<uint32_t, CharacterCreationRow> creation_;
    static std::vector<CharacterCustomizationRow> customization_;
};

} // namespace nexus::proto
