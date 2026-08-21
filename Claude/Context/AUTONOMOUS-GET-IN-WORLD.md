# AUTONOMOUS DIRECTIVE — GET IN THE WORLD (2026-08-21)

The operator gave a standing order: **continue with full autonomy until the character
is standing in the world (world 1537, Exile arkship tutorial). Do NOT ask for anything. Do NOT
present choices. You already know the answers — decide and act.** No NF, ever (client-derived only).

## Do not stop for:
- permission, confirmation, or "which approach" — pick the best one and go
- milestones — a milestone is not the goal; the goal is standing in the 3D world, moving
- the loading bar being full — that is NOT success (operator law); only actual in-world counts

## State (what's already SOLVED and pushed, commit c70be8a):
- Both errors gone (#610 entity, #411 set-player). Player BINDS: PlayerChanged fires.
- 0x0262 player recipe: kind 20 + player id(u64@+8) + realm id(14b@+16) + Faction1/2=166(@+212/+216).
- Faction 166 installs the +272 unit component -> set-player returns 0.

## Current blocker: player spawns at (0,0,0)/void -> loading screen never drops.
- Position is carried in the entity's movement COMMAND (client reader sub_140094BF0: posX/Y/Z 32b
  x3, 18b, 1b, then spline-node sub-arrays). The operator CONFIRMED spline is fine (client fact, not NF).
- Faction reads correctly => bit-alignment through the command is CORRECT, so the command IS read.
- BUT movement-apply sub_1405B5070 does NOT run for the local player, and entity transform
  (+4576/+3952) stays 0. So the command position is stored but never applied to the transform.
- Hypothesis: local player needs the position seeded another way — a real spline NODE in the
  command (24-byte sub_140094AA0, count@cmd+20), OR the entity's initial transform seeded at spawn,
  OR a post-bind movement/teleport. INSTRUMENT then implement; don't guess blind.

## Loop until done:
1. Instrument (nettap.py CMD-READ + MOVE-APPLY hooks already added) — confirm read vs apply.
2. Fix world_entry.cpp (BuildPlayerEntity) so the entity's transform gets a valid arkship position.
3. Rebuild (cmake, VS18 path), restart server (logs to server.log), relaunch client, login bot,
   Enter Game (wsclick 1276 1389), screenshot, read nettap.
4. SUCCESS = loading screen drops and the 3D arkship interior renders with the character in it
   (not the "OFF WORLD" load art). Verify by screenshot showing gameplay, not a load screen.
5. When in-world: commit + push (privacy-guard + nf-guard first), update session log + memory,
   leave the stack UP.

Login cycle scripts in <scratch>: wslaunch.ps1, wslogin.ps1, wsclick.ps1 x y, ws-shot.ps1 out.
cmake: add VS18 CMake bin to PATH then `cmake --build cpp/build --config Release --target nexus_realm`.
Client boots ~75s to login. Server = nexus_realm.exe in cpp/build/Release (start with output redirect).
