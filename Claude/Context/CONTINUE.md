# CONTINUE HERE — read first after any compact (2026-08-22)

Clean-room C++ engine (`nexus_realm`) driving the retail client through a polished proving-ground
world entry (char-select → 3D arkship), server-native, zero NF. Goal: flawless entry — gear shows,
actions show, all UI works, no UI errors, correct appearance, standing pose, real stats.

## STANDING DIRECTIVES (always in force)
- **CONTINUOUS MODE** — don't ask, don't stop to report; keep working until it's all fixed. [[continuous-mode-ruleset]]
- **NO NF, NO corpus.** Derive only from: the client (WildStar64.exe + CDN deconstruct in
  `Project Resources/_Client-RE/.../IDA/functions/`), our data/DB, the Jabbithole + wiki archives,
  web only if needed. [[never-touch-nf]]
- **STRAIGHT CODE ONLY** — no build-note/RE comments in source; notes go in `cpp/docs/CODE-NOTES.md`.
  Preserve MIT headers. [[pure-code-scrub-discipline]]
- **FULL-SUITE privacy sweep before EVERY commit/push:** `python provenance/privacy-guard.py` (no
  args), require EXIT=0. [[full-suite-privacy-sweep]]. Commit as chaosfox26-ai, empty email.
- **Push:** standing authorization ONLY for privacy-leak fixes ([[github-privacy-purge]]); otherwise
  never push unless asked.
- **Starlight Protocol** — use the hardware + local tools; build tools as needed (`tools/`).
- Leave realm + client UP when pausing.

## BUILD / RUN / TEST
- Build: kill `nexus_realm.exe`, then VS18 cmake `--build cpp/build --config Release --target nexus_realm`.
- Run: `cpp/build/Release/nexus_realm.exe` (WorkingDirectory = that dir), stdout → `%TEMP%/claude/server.log`
  via `-RedirectStandardOutput`.
- Relog loop: kill+`wslaunch.ps1` client, wait ~45s, `wslogin.ps1`, `wsclick.ps1 1276 1388` (Enter Game),
  wait ~30s. Tools in `%TEMP%/claude/`: wsvk.ps1 <vk> <ms> (P=char panel, Esc=close), ws-shot.ps1 <name>,
  uimon.py (event/dispatch monitor), pose_probe.py, hwwatch_pose.py.
- Test char: Peryanna, characterdb id 32, class 7 Aurin F, world 1537, activePath 1.

## DONE + committed + verified (GitHub clean)
- Server-driven ("ProactiveEntry") world entry — clean every relog; character in-world.
- Appearance correct in BOTH char-select and in-game (tools/loadout.py resolves gear→display from
  client tables → character_appearance rows).
- Char-select path icon (char-list 0x0117 3-bit path from DB activePath).
- Action bar restored (0x111 weapon-equip, slot 16 → IsWeaponEquipped).
- Persistent DB-driven gear; character-state save on disconnect (lastOnline/worldId); opcode reconcile.
- **PathTracker addon error FIXED** — bind the player DURING loading (proactive entry) so
  GetPlayerPathType is valid at addon setup. Verified error-free on repeated relogs.
- Straight-code scrub complete for session files; 8 dead functions + unused fields removed; notes in
  CODE-NOTES.md. Privacy: operator name purged from public git history (filter-repo + force-push, 0 left).
- Tools: client_tbl.py (parallel .tbl reader, validated 7/8 vs engine dumps), loadout.py.

## KEY MECHANISM: ProactiveEntry (cpp/src/realm/world_handshake.cpp)
On 0x07DD, after the first 0x00AD, `SpawnDelayed(1500ms)` runs the WHOLE entry during loading with a
server-chosen guid (0x0A000000|charId; send 0x0636 expectedPlayer BEFORE 0x0262 → auto-bind):
0x00AD-2nd, 0x00F1, 0x0636, 0x0262, 0x019B, 0x06BC, items(0x111), 0x025E; then +2500ms 0x0061 +
keepalive. The 0x038C handler early-returns when ProactiveEntry. (Client accepts timer-sent world
msgs pre-move#1 — proven.) Messages in cpp/src/proto/world_entry.cpp.

## REMAINING — deep-RE frontier (each a dedicated dive)
1. **Abilities on bar** — ability-BOOK add works (0x111 loc-type-4 → sub_140608C60, fires
   AbilityBookChange) but the BAR (LAS/ActionSetLib) is separate and stays empty. Disproven: 0x025E
   count2 (ability collection [32b spell/18b base/5b slot/5b tier]) does NOT feed the bar. 0x025E
   arrays: count1=items, count2=ability collection, count4=[14b][32b]. sub_140608C60 links spell→the
   item that grants it (item cache session+2704). NEXT: find the LAS-assign server message (getdesc /
   trace ActionSetLib store) + the correct class-7 LAS spell ids (spell4Base has no class column —
   try Jabbithole/wiki). LAS slots lock by level (correct at lvl 1; slot 0 is open).
2. **Standing pose (ACTIVE — deep dive, 2026-08-22)** — she renders LYING + movement-locked in
   EVERY world (verified in Everstar Grove 990, so NOT the arkship intro). All of it measured live
   with Frida against the running client (tools in %TEMP%/claude, see below). **Old notes here were
   WRONG and are disproven:** +4896 is a velocity-blend index (hammered it to 0 for 3600 writes, no
   effect); +440 is the STAND-STATE not HP (GetStandState=sub_140656560 reads entity+440); real HP is
   unit+444/+464 = live 250/250 (she is NOT dead/downed).
   **RULED OUT by measurement:** intro, HP/death, player-bind (0x019B handler sub_1403B5AD0 sets
   +120 player-unit +25744 container, fires PlayerChanged — all good), stand-state (+440=0 Stand;
   forcing Sit/Stand transitions changes the flag but NOT the visible pose), the spline node (cleared
   the live +3936 node — still frozen; red herring — DON'T write live spline ptrs, that CRASHED the
   client once), and camera/gameplay-mode (**camera WORKS** — operator confirmed mouse-look + zoom;
   only the CHARACTER is locked).
   **KEY measured facts:** (a) a SECOND, UNBOUND copy of the entity ALSO lies down → the lying is the
   DEFAULT IDLE animation applied to ANY entity my 0x0262 creates, not a player-control thing. (b)
   Emotes /sit /stand flip +440 (0→1→0) and even call the play-animation fn, but her body does NOT
   visually change → her MODEL ANIMATION CONTROLLER appears FROZEN at the initial lying frame. (c)
   Writing unit+4576 position live does NOT stick (held by an interpolator).
   **Entity data is CORRECT** — hooked the client's own reader sub_140096FA0 (op 0x0262/610, struct
   size 288): propCnt=1{id12 Health type2 250/250}, movCnt=1 type2 position→exact spawn coords,
   faction 166/166, all 3 optional tail-selectors 0. Full field map in the session log.
   **CURRENT LEAD / NEXT:** confirm the model animation controller isn't ticking for created entities —
   run `%TEMP%/claude/anim_tick.py` (hooks per-frame anim update sub_1405B5070 + play-anim
   sub_140474400, counts calls for the player unit). If sub_1405B5070 never fires for her → her anim
   isn't ticking (root); trace why a world-created entity's animation SET doesn't link (char-select
   force-loads it and STANDS with the same model). Key fns: entity reader sub_140096FA0; SetStandState
   sub_14045BF30 (server op 0x93C [u32 guid][u32 state][u32 data]); emote/anim applier sub_1404739B0 →
   sub_140474400(unit,animId,flag); movement apply sub_1404586E0. EModelSequence enum in
   client-ui/LuaDocData/data.xml (DefaultStand/PistolsStand/... but Lua ids, not internal values).
   **Code already in tree (harmless, did NOT fix pose):** BuildStandState/OpSetStandState 0x93C sent
   in the +2500ms block after 0x0061; movement position-keyframe trailing bit set 1 (settle). The
   diagnostic 2nd-entity spawn was added then REMOVED. TWID is back to 1537.
   **Operator collaboration mode:** the operator chose "you drive the client" — they test hands-on, I
   instrument in parallel. Probes: watch_live.py, dump_entity.py, anim_tick.py, spline_probe.py,
   health_scan.py, locostate.py, call636.py, pose_hammer.py, move_test.py (all in %TEMP%/claude).
3. **Real HUD stats** — HP is gameFormula-derived (250 placeholder ok for lvl 1); resource/other stats
   via unit-property ids (sub_140458140: id12=Health cur/max; ids 1-25 = other stats).

## LIVE STATE (2026-08-22)
Realm UP; client relaunched and in-world on the arkship (lying/frozen — the open pose bug).
Build cmake is at `C:/Program Files/Microsoft Visual Studio/18/Community/Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe`.
Full session detail: Claude/Context/SESSION-2026-08-21-world-entry.md (see the 2026-08-22 pose section).
Frida driving-loop lesson: read-only probe live pointers; writing a live spline node ptr crashed the client once.
