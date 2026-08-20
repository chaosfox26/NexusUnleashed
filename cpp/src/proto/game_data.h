#pragma once
#include <cstdint>
#include <map>
#include <string>
#include <vector>

namespace nexus::proto {

struct CharacterCreationRow {
    uint32_t Id = 0, ClassId = 0, RaceId = 0, Sex = 0, FactionId = 0, StartEnum = 0;
    std::vector<uint32_t> Items;
};

struct CharacterCustomizationRow {
    uint32_t RaceId = 0, Gender = 0, SlotId = 0, DisplayId = 0;
    uint32_t Label00 = 0, Value00 = 0, Label01 = 0, Value01 = 0;
};

struct AppearanceVisual { uint32_t Slot = 0; uint32_t DisplayId = 0; };
struct CustomizationChoice { uint32_t Label = 0; uint32_t Value = 0; };

class GameData {
public:
    static size_t LoadCharacterCreation(const std::string& tsvPath);
    static const CharacterCreationRow* Creation(uint32_t id);

    static size_t LoadCharacterCustomization(const std::string& tsvPath);
    static std::vector<AppearanceVisual> ResolveAppearance(
        uint32_t race, uint32_t gender, const std::vector<CustomizationChoice>& choices);

private:
    static std::map<uint32_t, CharacterCreationRow> creation_;
    static std::vector<CharacterCustomizationRow> customization_;
};

} // namespace nexus::proto
