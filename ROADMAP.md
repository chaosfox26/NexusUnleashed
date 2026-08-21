# ROADMAP — NexusUnleashed clean C++ engine (the north star)

_A clean-room, MIT, from-scratch WildStar 16042 server + engine that owes NexusForever nothing —
derived only from the client + our own data. Public: github.com/chaosfox26/NexusUnleashed._
_Deeper: `build-notes.md` (state), `Claude\Context\CONTINUE.md` (resume), `Claude\Context\CPP-PORT-PLAN.md` (full vision)._

## Where we are
A real 16042 client goes **all the way from login to standing in the 3D world** against this engine:
login → realm → character list → the full creator → create → **enter the world** — the character
renders as a full body (Aurin female) in the arkship Medbay. The crypto/login gate AND the world-entry
gate that stop every emulator are both cracked. **All by hand from the client — zero NF, zero captures.**

## Phase 1 — LOGIN & CHARACTER  ✅ DONE
- STS/SRP login (game-SRP, little-endian) · encrypted channels · realm handshake · token handoff
- Character list (`0x0117`) · character creator · **character create** (`0x5CD5` → persist → result)
- Packet cipher (qword-CFB), containers, framing — all client-derived, byte-verified live

## Phase 2 — WORLD ENTRY  ✅ DONE (server-native, no Frida)
The character **stands in the world**, driven entirely by server messages on the realm connection
(the old "separate world server on 24000 / new keying" plan was wrong — it's the same connection).
The completion mechanism was fully reverse-engineered: the client tracks world-load readiness as a
**7-bit mask at `session+31560` that must reach `0x7F`**; its per-frame update only drops the loading
screen at exactly `0x7F`. The recipe that drives it (all client-derived, generated from our DB):
- [x] `0x00AD` world-enter (worldId + position) → client leaves char-select, loads the map
- [x] `0x00F1` world-entry init (all-zero body) → sets `session+25632=1`, unblocks mask bit `0x10`
- [x] `0x0262` **player entity** — kind-20 Player, `Faction=166`, position keyframe, **+ race/sex + item
      visuals** (`a3+176`, `[7b slot][15b displayId]…`) so the **body renders**, not a floating head
- [x] `0x019B` set-player → binds the player (`PlayerChanged`), installs the unit component
- [x] `0x0061` PlayerEnteredWorld → sets mask bits `0x20|0x40` → mask hits `0x7F`, load screen fades
- [x] `0x0845` loading-progress **keepalive** (timer, movement-independent) → kills the ~30s watchdog drop
- **Milestone reached:** the character stands in the arkship Medbay, server-native.

### Phase 2.5 — WORLD-ENTRY POLISH  ⏳ ACTIVE (small, well-scoped)
- [ ] Standing pose — she renders lying down; needs the stand-state / unit-alive flag on the spawn
- [ ] Exact floor Y (she clips slightly; the saved 85.53 sits below the medbay floor)
- [ ] Per-character appearance from the DB (race/sex/customisation/visuals) — currently hardcoded for one char
- [ ] Full face customisation (the `character_customisation` sliders into the Player-block arrays)

## Phase 3 — A LIVING WORLD
- [ ] Movement (client ↔ server), spline/heartbeat steady-state
- [ ] Entity streaming (creatures, NPCs, props) from world data
- [ ] Spells/combat, abilities, stats · quests · loot · vendors · chat/social · groups
- [ ] Persistence for all of it (the DB layer extends per system)
- **End-state:** playable — a character logs in, moves, fights, quests, on OUR engine.

## Phase 4 — THE BIG VISION (why it's C++)
- [ ] Modern renderer path: **FSR 1 (early win) → FSR 2/3/4, DLSS 3/4**, a **DX12** renderer
      (FSR2+/DLSS need motion vectors the engine must expose — an engine-side prerequisite)
- [ ] Engine/perf work (the operator's optimization vision drives this)
- [ ] Community content platform

## Standing rules (never bent)
NO NexusForever (client + our data only) · order of authority = client → our tree → corpus → web ·
`nf-guard.py` + `privacy-guard.py` pass before any push · commit `chaosfox26-ai` (empty email) ·
never push without being asked.
