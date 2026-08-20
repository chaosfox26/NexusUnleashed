# Session 2026-08-20 (part 2) — CHARACTER CREATION FROM CLIENT DATA + WORLD LOADS

**Checkpoint commit `1937044` on master (LOCAL, not pushed). Privacy + NF guards CLEAN.**

Continues SESSION-2026-08-20-character-creator.md. This session took the real 16042 client
from "character screen" to **creating a character that saves as exactly what was built, and
loading into the world** — every fact read from Carbine's own client, zero NexusForever.

## What fell this session
1. **The realm-lane packet cipher** (the wall that hid everything past login). The client
   re-keys to a FIXED realm-lane key **`0x9A868DE642EF9906`** the instant it sends `0x058F`;
   we were still using the auth key, so create/delete/enter decoded to garbage (the "0x5CD5"
   create opcode was a ghost — the real one is `0x025C`). Recovered the key CLEAN from the
   client's own cipher object (Frida dump of key-table @+0x28, inverted through our key
   expansion; reproduces byte-for-byte across sessions ⇒ fixed constant). Fix: re-key
   `s.crypt` on `0x058F`. Spec: `spec/protocol/realm-lane-rekey.md`.
2. **Character creation** (real client → engine → DB): parse `0x025C` (name + creation id),
   resolve identity from the client's own `CharacterCreation.tbl`, reply `0xDC`. Character
   persists and lists. Spec: `spec/protocol/character-create-0xDC.md`.
3. **THE KEY METHOD (operator law):** the create packet carries a **CharacterCreation
   ID** (u32 @ body offset 6), not raw race/class. It resolves through the client's OWN table
   into race/class/sex/faction/startEnum/starting-items. Client tables are uncopyrightable
   FACTS → NF-proof AND authoritative. **No shortcuts, no DB hand-patching, no 2-sample diffs
   — read the truth from the client files.** Row 511 = Aurin/Spellslinger/Female/Exile,
   confirmed live in the client's char list (Spellslinger icon + Exile faction).
4. **World entry**: Enter Game on a character sends `0x07DD` (u64 charId). Replaying the
   recorded world-load burst (651 msgs) makes the client LEAVE char-select and LOAD INTO THE
   WORLD (loading screen + streaming `0x038C` movement). Spec: `spec/protocol/world-entry.md`.

## The engine code (cpp/)
- `crypto/packet_crypt.h` — `RealmLaneKey`. `realm/world_handshake.cpp` — re-key on `0x058F`;
  `0x025C` create handler; `0x07DD` world-replay handler.
- `proto/character_create.{h,cpp}` — parse `0x025C` (name + `CreationId`) + build `0xDC`.
- `proto/game_data.{h,cpp}` — loads `data/character-creation.tsv` (exported from the client
  `.tbl` via `tbl_reader`); `GameData::Creation(id)`.
- `db/db_store.{h,cpp}` — `CreateCharacter`. `realm/main.cpp` — wires it all + loads the tsv +
  the world-entry replay (`captures/world-entry-replay.bin`, gitignored/local).

## NEXT — character-record FIDELITY (the big one, deferred for a compact)
The char-list record (`proto/character_list.cpp`, opcode `0x0117`) sends an EMPTY appearance
and NO outfit → characters render as a black silhouette, and level shows wrong. Fix, all from
client tables:
- **Appearance:** the create packet's slider block (after the name: `40 00 00 00` then u32s) is
  the player's (label→value) picks. `CharacterCustomizationLabel.tbl` (27 sliders) +
  `CharacterCustomization.tbl` (4784 rows: per (raceId,gender), (labelId,value) → itemSlotId +
  itemDisplayId). Decode the block → store → echo in the record (the client resolves+renders).
  Aurin have the most to customize (ears/tail/fur). First: pin the block's exact layout.
- **Outfit:** the 7 starting `itemId`s from the CharacterCreation row → `ItemDisplay.tbl` /
  `Item2.tbl` → the displayed gear.
- **Record layout:** fix `character_list.cpp` field positions AUTHORITATIVELY (level etc. were
  placed by inference) — from the client's char-record deserializer, not guessed.
- **Verify for REAL:** a fresh create must decode correctly through the CODE (not a DB patch).
  Needs a free slot → also address the 2/2 slot cap + delete (`0x0352`, currently unanswered).

## Also open
- Path field in the create packet not yet pinned (Soldier=0 so it hid in zeros).
- Real world entry = generate the starting zone from the client's world/zone/spawn tables
  (startEnum → starting zone), the **Arcterra method** — supersedes the capture replay.
- World-load replay stalls at the loading screen (burst truncated at first heartbeat + we
  ignore the client's `0x038C`); the sustainable fix is the table-driven world above.

## Stack state at checkpoint
Clean-engine server `nexus_realm` UP (Release, port 24000 realm-lane, 23115 auth, 6600 STS;
MariaDB 3307). Char-creation tsv + world-replay loaded at boot. Client's characterdb: account 2
has char id 22 + char id 30 (Aurin Spellslinger — note: id 30 was hand-fixed in DB
this session, which the operator flagged as artificial; the CODE path is what must be verified next).
Frida tooling in `<scratch>/` (cipher-dump, seed-derive, full-probe-wait, ws-shot.ps1).
