# Session 2026-08-20 (part 3) — CHARACTERS RENDER · REALM SELECT · THE LAUNCHER · OPTIMIZATION

Continues SESSION-2026-08-20-character-creation.md. This session took the clean engine from
"a character is created" to **a character that persists, renders correctly, and can be managed
(create/delete/realm-select) — all live in the real client** — then built the **Nexus Unleashed
Server Launcher (nusl.exe)** and wrote the **OPTIMIZATION.md** manifesto. Everything derived from the
16042 client + our own work; **0 lines of NexusForever.**

**All commits pushed to github.com/chaosfox26/NexusUnleashed (public) through `e5e0c00`.** Author
`chaosfox26-ai <>`. Privacy-swept before every push (guards CLEAN, commit messages clean).

## 1. The character appearance system — PROVEN IN-GAME (I drove the client myself)

Operator granted full control to drive the retail client ([[drive-client-autonomously]]). Created
a test character (Aurin/Spellslinger/Female) live and she **renders** with her chosen **cat ears +
gold hair + pale skin**, screenshot-proven at char-select. The pipeline (all client-table-derived):

- **Create packet appearance block** (0x025C body, after the name): `[count][labelId×count]
  [value×count]`, each u32, real value = `(u32 >> 3)` (low 3 bits = tag). Table-validated.
- **Resolver** `(race,gender,label,value) → (slot,displayId)` via `CharacterCustomization.tbl`;
  reproduces a real stored character's `character_appearance` rows exactly.
- Wired: `GameData::LoadCharacterCustomization` + `ResolveAppearance`; `CharacterCreateRequest`
  parses the block; `DbCharacterStore::CreateCharacter` persists `character_customisation` + resolved
  `character_appearance`; `GetCharacters` serves them; `CharacterListMessage` emits countA visuals.
  Spec: `spec/protocol/character-appearance.md`.

## 2. Char-select protocol — fixes, all verified live

- **THE CONTAINER FIX (big one).** Post-re-key the realm lane is the world channel: S→C rides the
  **0x03DC** container, NOT the pre-re-key 0x0076. Delete result (0xE6), create result (0xDC), and
  list refresh all now ride 0x03DC. This is why the create result previously only landed "on
  reconnect." Offline-verified with a python protocol client (wsclient.py cipher) before the live test.
- **DELETE** (0x0352 → 0xE6 code 0): client sends msg 850 (u64 charId); server soft-deletes
  (deleteTime, account-scoped) and replies 0xE6; client removes it live. Verified (deleted both test characters).
- **LEVEL/WORLD/FACTION fields** were each one slot off in the record: nLevel=+0x20, idWorld=+0x1c,
  idFaction=+0x6c (a char showed faction id 167 as its level). Verified: levels read right.
- **REALM SELECT**: 0x07A4 (realm-list request from the "Change Realm" screen) → reply 0x0761 with
  the realm entry (Evindra). 0x07DF (Enter Realm) → serve 0x0117 char list (was hanging on
  "Retrieving Characters"). Realm **Status = 4 (Up)** → green checkmark (was 0/Unknown = "?").
  Realm **PvpType = 2 (RP-PvE)** data flag set (restoration-ready).
- Full char-select window swept live and WORKS: Credits, EULA, Configure, Change Realm, Enter Realm,
  Log Out, Create, Delete, level/faction/appearance. Unlock-slots is an inert store feature (no
  storefront). Client launch: [[drive-client-autonomously]].

## 3. STILL OPEN (clean engine)

- **The OUTFIT.** Customization renders; equipped starting GEAR does not (character shows in
  underwear). Gear = the `item` table (location 0 = equipped); a created char has none. Fix: on
  create, insert the CharacterCreation row's starting items as equipped; serve their visuals in the
  record's **countB**. BLOCKER: gear itemDisplayId is a multi-table resolve (Item2.itemDisplayId is 0;
  real display = Item2 itemSourceId+item2TypeId → ItemDisplaySourceEntry by level range → displayId).
  Client tables at `_Arctium/.../CSV/WildStar_TBL_16042/*.csv`.
- **RP-PvE literal text.** The 16042 client hardcodes PvE/PvP in its packed UI archive; showing
  "RP-PvE" needs a ClientData.archive (13 GB) repack — its own focused project. Tooling identified
  (NexusVault CLI, wildstar-studio — NF-free). Server side is already restoration-ready (PvpType=2).
  Full plan: `Claude/Context/RP-PVE-CLIENT-UI-BLOCKER.md`.

## 4. nusl.exe — the Nexus Unleashed Server Launcher (NEW)

`cpp/src/launcher/nusl.cpp` (+ `nusl.rc`, `nusl.ico`), CMake target `nusl` → ships next to
nexus_realm.exe. **Native Win32 + GDI+, no .NET, no WebView2, tiny.** Operator chose native-GDI+ over
WebView2 for leanness. Features (all verified live):
- Start / stop the server, live status pill, dark log tail (reads nexus_realm.log).
- **Resource governor:** MEMORY CAP slider (1..N GB) enforced by a **Job Object**
  (JOB_OBJECT_LIMIT_JOB_MEMORY + KILL_ON_JOB_CLOSE); CPU CORES slider → process **affinity mask** +
  the server's worker-pool size via **NUSL_THREADS** (main.cpp reads it: "worker pool: N threads").
- **Live CPU + RAM meter bars**, active by default (500ms poll; GetProcessMemoryInfo + GetProcessTimes).
- Reads realm name (Evindra) + ports from realm.json. Palette magenta/blue/black/white (operator's).
- Polish: gradient wordmark, background glows, rounded panels, custom gradient sliders w/ glowing
  thumbs, gradient buttons, **dark title bar** (DwmSetWindowAttribute), **dark scrollbar**
  (SetPreferredAppMode AllowDark + SetWindowTheme DarkMode_Explorer), the operator's emblem as the
  app icon. Screenshot in README (`docs/launcher.png`), shown under the roadmap.

## 5. OPTIMIZATION.md — the performance manifesto (NEW, public)

A permanent statement of intent: the engine is built to be the leanest/fastest WildStar server ever
and to run **the entire game (~2,760 worlds) resident + ticking in parallel** under a dialable memory
budget across every core. Principles: measure-not-guess, data-oriented, **memory compression** as a
first-class tool, zero hot-path allocation, **multicore-first** (proven scaling on a Linux realm),
native/no-GC, the launcher-as-measurement-tool, and the **no-reading-NF** line. **§7: maps
compressed + cached + demand-loaded** — in Everstar Grove, Algoroc isn't loaded; a warm cache of
likely-next maps (starting zones, entered world, adjacent zones) makes startup near-instant.
See [[operator-strength-is-optimization]] — optimization is the operator's domain; Claude implements.

## 6. Roadmap / README (public)

Milestone rewritten to "a character persists and renders." Phase 06 expanded (persist+render+manage);
Phase 07 corrected to 0x025C. Roadmap SVG recolored to **magenta/blue/black/white** with a clean
5-point North Star. Launcher screenshot added under the roadmap.

## 7. State at checkpoint

Clean-engine `nexus_realm` **running under nusl.exe** (4 GB cap, 32 cores). MariaDB 3307 up. Account
2 characterdb: both test characters were deleted this session (clean slate for testing) — soft-deleted,
recoverable. Retail client currently closed/at login. Frida + capture tooling in `<scratch>/`.
Screenshot hygiene rule in force ([[drive-client-autonomously]]): delete each ws/nusl screenshot
right after use.
