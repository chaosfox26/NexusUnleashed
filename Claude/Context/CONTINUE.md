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

## ACTIVE TASK — FIX PATHTRACKER (operator: "I want that add-on back up and running fully without errors")
ROOT CAUSE (fully proven): PathTracker.lua ResizeAll (line 726) NREs on nil `wndActiveHeader`.
PathTrackerSetup builds that header only if `PlayerPathLib.GetPlayerPathType()` != nil, and that getter
needs the BOUND PLAYER (path natives read session+120 = player unit; see client sub_140398470 line 12).
Our movement-gated entry binds the player at move#4 (needs the client's 0x038C guid), but the addon's
OnObjectiveTrackerLoaded -> PathTrackerSetup runs during the black LOADING screen, BEFORE the client
sends its first 0x038C (move#1). The client DROPS all world messages until move#1 (0x07DD-time sends are
dropped). So no message-timing or bind-reorder fixes it — TESTED: reorder, early-bind@move#1, char-list
path, 0x07DD-early-send all still error. It's a structural ordering wall.
THE FIX (in progress): SERVER-DRIVEN ENTRY — don't wait for client movement. After 0x07DD's first
0x00AD, on an asio steady_timer delay (copy StartKeepalive's timer pattern, game_server.cpp:54), send
the whole sequence with a SERVER-CHOSEN guid (the 0x0636 expectedPlayer fallback lets us pick the guid:
send 0x0636(guid) so the client stores expectedPlayer, then 0x0262(guid) auto-binds): 0x00AD-2nd +
0x00F1 + 0x0636 + 0x0262 + 0x019B + 0x06BC(path) + items + 0x025E + 0x0061 + keepalive — all during
loading, before the addon loads. Put it behind a flag (like WorldChangeDoneEnabled) = ProactiveEntry so
it's A/B testable and reversible; keep the movement path as fallback. RISK: reorders the WORKING entry
(dressed/action-bar/gear all currently work off the movement path) — test incrementally, git revert if
it breaks. If the client drops the timer-sent messages too (like 0x07DD), this avenue is dead and the
fallback is: accept the one-time error OR find how retail delivers path in the pre-addon player state.

## OTHER OPEN POLISH (after PathTracker)
- POSE: she renders lying down = a StandState (client enum Stand/Sit/LyingDown/DeathPose/... via
  GetStandState). HP is fine (250/250, probed) so NOT death. Setter is name-stripped; trace
  GetStandState's native -> unit-struct offset -> find the entity field / ServerUnit* msg that sets it;
  set Stand(0). Likely a top-level 0x0262 field (selectors/tail) or a unit property. Arkship "wake up
  unconscious" intro state.
- ABILITIES ON BAR (#11): 0x111 location-type-4 = ability-book add (fires AbilityBookChange); need
  class-7 (Spellslinger) spell ids from Spell4/Spell4Base + the LAS-assignment message.
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
