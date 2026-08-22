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
2. **Standing pose** — renders lying. `unit+4896` (unit=session+120) = render stand-state = 2
   (LyingDown), re-applied per tick by the ANIMATION system (writer +0x5b81fb in sub_1405B5070, called
   from the unit update +0x1c8add/+0x1c8991) — it's COMPUTED from an upstream authoritative pose, not
   directly settable. HP is fine (not death). NEXT: watchpoint/trace the anim INPUT → the authoritative
   stand-state the server sets (a ServerUnit* msg or 0x0262 entity data) → set Stand(0).
3. **Real HUD stats** — HP is gameFormula-derived (250 placeholder ok for lvl 1); resource/other stats
   via unit-property ids (sub_140458140: id12=Health cur/max; ids 1-25 = other stats).

## LIVE STATE
Realm UP; client may be closed (relaunch with wslaunch.ps1). Everything committed, remote clean.
Full session detail: Claude/Context/SESSION-2026-08-21-world-entry.md.
