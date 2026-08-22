# CONTINUE HERE — read this first after any compact (2026-08-22)

## STANDING DIRECTIVES FROM THE OPERATOR (verbatim intent — always in force)
- **CONTINUOUS MODE. Do NOT ask questions. Do NOT stop to report.** Keep working until ALL of the
  proving-ground polish is fixed. "Keep going until all of this is fixed."
- **NO NF. NO corpus.** Clean-engine code derives only from the client (WildStar64.exe deconstruct +
  CDN deconstruct), our own data/DB, the Jabbithole archive, the wiki archive, and web search if
  truly needed. Never open NF source/servers/captures.
- **STRAIGHT CODE ONLY.** Source files must be plain code — NO build-note / RE-explanation comment
  essays in the code. Put build notes / RE findings in an MD file: `cpp/docs/CODE-NOTES.md`
  (and `docs/CODE-NOTES-csharp.md` for C# refs). Preserve MIT headers. See [[pure-code-scrub-discipline]].
  → PENDING TASK: scrub the heavy inline comments I added this session (world_entry.cpp/.h,
    world_handshake.cpp, db_store.cpp/.h, game_server.h, character_list.cpp) into CODE-NOTES.md.
- **Starlight Protocol:** use the machine (32 threads / RTX 5090 / RAM) and local tools; build
  whatever tools are needed. Tools live in `NexusUnleashed-Engine/tools/` (client_tbl.py, loadout.py).
- **FULL-SUITE privacy sweep before EVERY commit AND push:** `python provenance/privacy-guard.py`
  with NO file args, require EXIT=0. See [[full-suite-privacy-sweep]]. Commit as chaosfox26-ai, empty
  email. Never push without being asked.
- Leave the realm + client UP when pausing. Build: kill nexus_realm.exe, VS18 cmake --build build
  --config Release --target nexus_realm; run cpp/build/Release/nexus_realm.exe (WorkingDirectory =
  that dir), stdout -> %TEMP%/claude/server.log via -RedirectStandardOutput.

## THE GOAL: a completely NF-clean, fully-polished proving-ground entry
Char-select + in-world must be flawless: gear shows, actions show, all UI works, NO UI errors, correct
appearance, standing pose, real stats. "This is the beginning of the game where I prove AI can do as
good as NF and better, without them."

## DONE + committed this session (all verified live)
- Appearance BOTH screens (dressed) — char_appearance fed by tools/loadout.py resolution. (388fdf6)
- Char-select PATH ICON now shows — char-list 0x0117 3-bit path field wired from DB activePath.
- Action bar restored (0x111 weapon equip, slot 16). (670a7a6)
- Persistent gear (DB-driven item stream on entry). (b7536bc)
- Opcode reconcile (#10 done) + server-side character save on disconnect (lastOnline/worldId). (f3e2d95)
- Tools: client_tbl.py (validated 7/8 vs engine dumps) + loadout.py. (388fdf6)

## PATHTRACKER — FIXED (2026-08-22) via SERVER-DRIVEN (PROACTIVE) ENTRY
The stock addon's PathTrackerSetup runs during the black loading screen (before the client's first
0x038C) and needs GetPlayerPathType() != nil, which needs the BOUND PLAYER (path natives read
session+120). The movement-gated entry bound the player too late (move#4), so setup bailed and the
resize timer NRE'd. FIX: `ProactiveEntry` flag (world_handshake.cpp) — on 0x07DD, after the first
0x00AD, a `SpawnDelayed(1500ms)` coroutine sends the WHOLE entry during loading with a SERVER-CHOSEN
guid (0x0A000000|charId; 0x0636 expectedPlayer sent before 0x0262 so it auto-binds): 0x00AD-2nd +
0x00F1 + 0x0636 + 0x0262 + 0x019B + 0x06BC + items + 0x025E, then +2500ms 0x0061 + keepalive. The
0x038C handler early-returns when ProactiveEntry. VERIFIED: no PathTracker error, entry complete,
dressed, action bar up. (Proven the client accepts timer-sent world msgs pre-move#1 via a probe:
W-DISP 0xad at +1.5s before the movement sequence.) game_server.SpawnDelayed is the timer helper.

## ACTIVE TASK — remaining proving-ground polish (continuous)

## OTHER OPEN POLISH (after PathTracker)
- POSE: she renders lying down = StandState. CONFIRMED offset via live probe: **unit+4896 (session+120)
  = the render stand-state = 2 (LyingDown)**. HP fine (250/250) so NOT death. The client RE-APPLIES 2
  every tick (Frida writes of 0 were overwritten x49) -> it's maintained from an authoritative source,
  not a stale field. Readers that branch on ==2: sub_1403AFB10:272, sub_1403B2240:154 (animation
  select). Static writer-search is noisy (offset 4896 collides across structs - it's an anim blend
  index in sub_1405B5070). NEXT (dedicated pass): find the AUTHORITATIVE stand-state field (copied to
  +4896 each tick) + where the entity construction / a ServerUnit* message sets it; set Stand(0). Likely
  set from the 0x0262 entity data (default 2) or a unit property/emote. Arkship intro "unconscious" state.
  Probes: %TEMP%/claude/pose_probe.py, pose_set.py. HW-WATCHPOINT (hwwatch_pose.py) caught the writer:
  instruction +0x5b81fb (in sub_1405B5070, the ANIMATION blend/state computation), called per-tick from
  +0x1c8add/+0x1c8991 (unit update). So +4896 is COMPUTED by the anim system from an upstream
  authoritative pose - not directly settable. NEXT: watchpoint/trace the anim system's INPUT (the
  authoritative stand-state the server sets via a ServerUnit* message or the 0x0262 entity data) and
  set Stand(0). Deep animation-system dive.
- ABILITIES ON BAR (#11): ability-book add = 0x111 location-type-4 (a2[5]==4 branch of sub_1403B77D0
  -> sub_140608C60(slot,spellId) + fires "AbilityBookChange"). But the action bar shows the LAS
  (ActionSetLib), a separate equipped set - adding to the book != on the bar. OPEN: (a) class-7
  (Spellslinger) starter spell4 ids - spell4Base.tsv has NO direct class column (class is via a
  prereq / a separate ability-book table; check Jabbithole archive + client ability tables), and (b)
  the LAS-assignment mechanism (does level-1 auto-slot from the book, or is there a server action-set
  message?). TESTED 2026-08-22: 0x111 type-4 x3 (spell 5872 Quick Shot) -> AbilityBookChange fired 3x
  (book updated) but the bar slots stayed EMPTY + LOCKED. Auto-slot DISPROVEN: book != bar. The bar's
  LAS slots are LOCKED at lvl 1 (slot 0 empty-but-available; slots 1+ padlocked = correct for lvl 1).
  TESTED 2: the 0x025E char-data blob's count2 array (element sub_14008CDF0 = [32b spellId][18b baseId]
  [5b slot][5b tier]) populated with (5872,5166,0,1) -> blob still parses (CharacterCreated fires) but
  NO ability on the bar. So count2 is an ability COLLECTION, not the LAS bar. 0x025E arrays decoded:
  count1 = items (same item struct sub_14008C0D0 as 0x111), count2 = ability collection [32/18/5/5],
  count3 = 2-byte elems, count4 = [14b][32b] elems (14b too small for spell4 ids -> not spells).
  NEXT (dedicated LAS dive): the bar reads ActionSetLib; find the actual LAS-assign server message
  (getdesc candidate opcodes / trace the client LAS store) - it's separate from the ability collection.
  Also need the correct class-7 LAS ability ids (5872 Quick Shot may be wrong/needs validation).
- REAL HUD STATS (#12): real class/level max-HP (currently placeholder 250) via the stat tables +
  unit-property ids (sub_140458140: id12=Health cur/max; ids 1-25 map other stats).
- PURE-CODE SCRUB: move this session's inline build-note comments into cpp/docs/CODE-NOTES.md.

## KEY FILES / FACTS
- Entry state machine: cpp/src/realm/world_handshake.cpp (0x07DD handler + 0x038C move-count gates:
  move#1 entity+path, move#4 bind+items+chardata, move#6 0x0061+keepalive). Spawn TWID=1537,
  TWX=1437.82 TWY=86.10 TWZ=-106.82.
- Messages: cpp/src/proto/world_entry.cpp (BuildPlayerEntity, BuildItemAdd 0x111, BuildSetPlayerPath
  0x06BC, BuildCharacterDataMinimal 0x025E, ...). char list: cpp/src/proto/character_list.cpp.
- Test char: Peryanna, characterdb char id 32, class 7 Aurin F, world 1537, activePath=1. Test acct
  login via %TEMP%/claude/wslogin.ps1. Drive: wslaunch.ps1, wsclick.ps1 x y, wsvk.ps1 <vk> <ms>
  (P=char panel, Esc=close), ws-shot.ps1 <name>, uimon.py (event/dispatch monitor), hp_probe.py.
- Client-drive loop: kill+wslaunch client, wait ~45s, wslogin.ps1, wsclick 1276 1388 (Enter Game),
  wait ~30s for world. Full session detail: Claude/Context/SESSION-2026-08-21-world-entry.md.
