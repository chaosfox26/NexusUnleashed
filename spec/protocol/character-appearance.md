# Character appearance — the char-select model, decoded from the client's own tables

Everything here was read from the 16042 client (its char-record deserializer + its
`CharacterCustomization.tbl`) and **validated against a real stored character**. Zero NexusForever.

## The char-list record wire layout (authoritative)

Read from the client's per-character deserializer `sub_14007F720` (WS+0x7F720). In wire order,
one character record is:

| field | bits/type | meaning | where it goes (via `sub_1400201F0` → `sub_1400235D0`) |
|---|---|---|---|
| id | u64 | character id | tChar (list index) |
| name | widestring | name | `strName` |
| A | 2b | sex | `idGender` |
| B | 5b | race | `idRace` |
| C | 5b | class | `idClass` |
| **D** | 32b | *(record+28)* | one of nLevel / idWorld / idFaction — **not yet pinned** |
| **E** | 32b | *(record+32)* | one of nLevel / idWorld / idFaction |
| **countA** | 32b + list | **appearance visuals** | each item → model visual array |
| **countB** | 32b + list | appearance visuals list 2 (+dye colors) | model visual array + dye array |
| F,G,H | 15b,15b,14b | record+64/+68/+72 | **+64/+68 are the two model-builder args** (`sub_140448BE0`) |
| pos | 5×float | position (record+76..92) | model position |
| I | 3b | record+96 | `idPath` (paths 0..3) |
| J,K | 1b,1b | record+100 / +104 | `bDisabled` / `bRequiresRename` |
| **L** | 32b | *(record+108)* | one of nLevel / idWorld / idFaction |
| countC | 4b + two u32 arrays | (≤7) → UI+720[], UI+748[] | (secondary) |
| countD | 32b + u32 array | → UI+776 | (secondary) |
| tail | float | record+152 | `fLastLoggedOutDays` |

**Each countA/countB item** is read by `sub_1400AB890` as `{7b, 15b, 14b, 32b}`. `sub_1400201F0`
stores `item[1]` (the 15b) into the model's visual array at index `item[0]` (the 7b, must be < 72).
So **item = {slot, displayId, dyeKey, dyeColor}**. countB additionally packs the 14b/32b into a
10-bit-per-channel RGB dye written to a parallel color array.

**Root cause of the black silhouette:** we sent `countA = countB = 0`, i.e. no visuals, so the
model had no skin/hair/eye/gear textures.

## The create packet's appearance block (0x025C body)

After the wide-string name, the create request carries:

```
[count][labelId × count][value × count]      — each a u32; the REAL value is (u32 >> 3)
```

The low 3 bits are a type tag (0 here). **Validated**: for creationId 511 (Aurin/Spellslinger/
Female/Exile) this decodes to count=8, labels `[1,2,3,4,7,10,16,25]` — exactly the Aurin-female
label set in `CharacterCustomizationLabel.tbl` — and values `[4,10,6,1,1,8,10,50]`, every one
inside its label's valid range in `CharacterCustomization.tbl`. Not a coincidence; it is the decode.

## Resolving sliders → visuals (`CharacterCustomization.tbl`)

Row columns: `raceId, gender, itemSlotId, itemDisplayId, label00, value00, label01, value01`.
For a character's chosen `(label → value)` map, a row for the character's `(race, gender)` **applies** when:

- `label00 == 0` (a base/always-on row), **and/or**
- `label00 != 0` ⇒ `chosen[label00] == value00`, **and**
- `label01 != 0` ⇒ `chosen[label01] == value01`.

Each applying row yields `(itemSlotId → itemDisplayId)`. Dedupe by slot (table order, last wins).

**Validated end-to-end:** running this on the 8 stored sliders of a real Aurin-female character
(id 27) reproduced her stored `character_appearance` rows **exactly** — all 7 slots
(24,25,26,27,28,39,70), exact displayIds, no misses, no ambiguity.

## Storage (inherited characterdb)

- `character_customisation (id, label, value)` — the raw chosen sliders.
- `character_appearance (id, slot, displayId)` — the resolved visuals the char-select model renders.

## The engine wiring (this change)

- `GameData::LoadCharacterCustomization` + `GameData::ResolveAppearance(race,gender,choices)`.
- `CharacterCreateRequest` parses the `[count][labels][values]>>3` block → `Customization`.
- `DbCharacterStore::CreateCharacter` persists `character_customisation` and the resolved
  `character_appearance`.
- `DbCharacterStore::GetCharacters` loads `character_appearance` into the record.
- `CharacterListMessage` emits countA visuals `{slot, displayId, 0, 0}`.

## Still open

- **D/E/L (record+28/+32/+108) = which is nLevel vs idWorld vs idFaction** — three 32-bit fields,
  order not pinned from the decompile (the immediates were optimized out of the `.c`). Confirm by
  reading the level/zone/faction the client shows for a served character (measurement).
- countB dye colors (only needed for dyed customization) — currently 0.
- The trailing create-packet field after the slider block (`>>3 = 46` for creationId 511) — a
  scalar (bone/face preset?), not yet needed for render.
