# ROADMAP — NexusUnleashed clean C++ engine (the north star)

_A clean-room, MIT, from-scratch WildStar 16042 server + engine that owes NexusForever nothing —
derived only from the client + our own data. Public: github.com/chaosfox26/NexusUnleashed._
_Deeper: `build-notes.md` (state), `Claude\Context\CONTINUE.md` (resume), `Claude\Context\CPP-PORT-PLAN.md` (full vision)._

## Where we are
A real 16042 client authenticates end-to-end against this engine and reaches **character creation**:
login → realm → character list → the full creator → **create a character** (persists to DB, renders
on reconnect). The crypto/login gate that stops every emulator is fully cracked. **All by hand from
the client — zero NF.**

## Phase 1 — LOGIN & CHARACTER  ✅ DONE
- STS/SRP login (game-SRP, little-endian) · encrypted channels · realm handshake · token handoff
- Character list (`0x0117`) · character creator · **character create** (`0x5CD5` → persist → result)
- Packet cipher (qword-CFB), containers, framing — all client-derived, byte-verified live

## Phase 2 — WORLD ENTRY  ⏳ ACTIVE (the whole current focus)
Build the **world server (port 24000)** so a character *stands in the world*. Each message is RE'd
from the client's own deserializer and **generated from our DB, per-character** (no captures, no NF —
the tainted replay was removed, commit `cdc62fc`).
- [ ] World-entry handshake on 24000 (new connection / keying)
- [ ] `0x0988` world payload — which map to load (from the character's WorldId)
- [ ] `0x0981` world-init id list
- [ ] `0x0117` **self block** — the player entity (guid, position, appearance)
- [ ] the "load complete" signal → drop from loading screen into the zone
- [ ] `0x0262` entity stream (can start empty)
- **Milestone:** the character stands on the ground in an (even empty) map.

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
