# SESSION 2026-08-21 — WORLD ENTRY: transport PROVEN, blocker = char-select→world transition

Goal (operator): get a character to load into the **current tutorial zone** (world 1537
`Map\ExileArkShipTutorial`), world entry built from scratch, **NO NF**. Gambler's Ruin parked.

## What was done (all NF-free, client-derived)

1. **Provenance pass on `world_handshake.cpp`** — it's ours (opcodes from the client dispatch tree,
   bodies from the client's deserializers + our DB, keys runtime-observed from the client's cipher
   object). `nf-guard` CLEAN. One flag logged: `HelloBody()`'s 47 literal bytes have capture-provenance
   (format is client-understood); reconstruct field-by-field. In `provenance/LEDGER.md`.

2. **RE tooling revived + validated (NF-free):** `deser.py` (client Read-fn decoder) retargeted to the
   consolidated client path; validated by reproducing the documented `0x0117` char-list tree exactly.
   `probe-all.json` = opcode→fn10/18/20 factory table (1121 entries; fn20 = Read RVA).

3. **World-entry message formats decoded from the client** (spec/protocol/world-entry.md
   "CLIENT-DERIVED MESSAGE FORMATS"): 0x0981 `[u32 count][u32 id…]`; 0x0988
   `[u32 n1][n1×{wstr,wstr,u32,u32,u32,1b}][3b][u32 n2][n2×{u32,wstr,u32,u32}]` (string-pairs, NOT
   coords — the "world payload" label was a size-guess); 0x098B `[u32 count][…]`; 0x0262 = large nested
   entity descriptor (position = 3×float32 inside the `WS+0x94BF0` command sub-block).

4. **Built `proto/world_entry.{h,cpp}`** (serializers for 0x0981/0x0988/0x098B; PacketWriter) and wired
   the `0x07DD` handler to emit a first candidate burst via `0x03DC`. Added to CMake. **Consolidation
   fix:** the C++ build tree had the pre-move source path baked into CMakeCache — reconfigured for the
   umbrella location.

## LIVE TEST (Frida-instrumented, operator's suggestion) — the decisive findings

Entered game as **Peryanna (char id 32)**, world set to 990 (Everstar Grove) with a known-good spawn.

- **`deep-trace.py` PROVES the client decrypts my world burst:** DEC hook shows `out=8809…`=0x0988,
  `out=8b09…`=0x098b, `out=8109…`=0x0981. **Transport/keying/container/decryption ALL WORK for the
  world stream** (same 0x03DC + RealmLaneKey path create-results use). The client RECEIVES my messages.
- **`dispatch-trace.py`:** the char-select dispatcher `sub_140020EA0` processed only `0x117` — my world
  opcodes never reached it. Correct: world opcodes belong to a **separate world message manager**.
- **Client stays at char-select.** RE of the Enter Game sender `sub_140023400`: it sends `0x07DD`
  (=2013), stores the pending char at `+376`/name at `+384`, sets char-select mgr **state (+368) = 4**,
  calls `sub_1400035B0`. The dispatch early-returns/ignores when state==5 or 6 (line 156040-43); state 4
  processes normally.

## THE BLOCKER (precisely scoped)

The client decrypts the world stream but its **world message manager is not active while in char-select
state 4**, so world messages are dropped. The client needs a **char-select→world transition** first
(the world stream comes AFTER). The old (removed) replay used `0x058F` (reconnect) which bypasses
char-select — that's why it reached a loading screen; the clean `0x07DD` char-select path needs the
transition.

**UPDATE — blocker narrowed to CONTENT (world-entry-trace.py, 2nd cycle):** hooked the world-opcode
Read fns. Result: **`READ 0x0988`, `READ 0x098B`, `READ 0x0981` ALL FIRED** — the client's **world
manager IS active and PARSES my messages**. The earlier "world manager inactive" hypothesis is WRONG.
`sub_1400481B0` did NOT fire. So: the client receives + decrypts + parses my world burst, but the
messages are EMPTY (count 0), so nothing drives the world load / no char-select→loading transition.

**NEXT:** send REAL content and find which message triggers the loading-screen transition. The client
parses 0x0988/0x098B/0x0981 (empty) without transitioning, so either (a) one of them needs specific
non-empty content, or (b) the transition is a LATER message (the player self-block + 0x0262 entity
stream). Method: RE the world manager's 0x0988 handler (what it does with the parsed content / on what
condition it initiates the map load), and/or find the loading-screen function and hook it. The
self/player block opcode (world-entry.md #9, mislabeled 0x0117=char-list; real opcode unknown) is a
prime suspect for the "you are entity X in world W" trigger. Iterate via Frida (operator's method).

**PROVEN so far (the milestone):** clean C++ engine sends a from-scratch, NF-free world burst that the
real 16042 client receives, decrypts, and parses. Transport + keying + container + framing + the
message wire-formats are all correct. Remaining = message CONTENT + sequence to drive the load.

## THE TRANSITION TRIGGER FOUND: 0x36A → reconnect-to-world (2026-08-21)

RE of the char-select dispatch `sub_140020EA0`: **opcode `0x36A` (874) is the char-select→world
transition.** Its case (line ~156094-156101) **CLOSES the client's realm connection** (`+184=0` +
vtable close), calls `sub_1400481B0` (creates a new game-state object via `sub_140033780`, target =
`*(qword_140C635F0+5888)`), and sets mgr state 6 (dispatch then ignores char-select msgs). Wire:
`0x36A` = a single `[5 bits]` status (client Read `sub_14007E950` = read-5-bits; deser.py missed the
width because it's an arg not a tracked reg).

**LIVE TEST (0x36A, status 0):** `sub_1400481B0` FIRED (confirmed by Frida hook) — the transition
works. BUT the client then showed **"You've lost connection with the server. Reason 0"** and did NOT
reconnect (no new connection reached port 24000; only STS pings continued). So:

**MODEL: world entry is a RECONNECT.** `0x07DD` (Enter Game) → server sends `0x36A` → client closes the
realm connection and reconnects to a WORLD SERVER, then sends `0x058F` there → the world stream flows on
the new connection. This matches the observed session-2 flow (which used `0x058F` reconnect). The
disconnect happened because **the world-server address was never set** (`qword_140C635F0+5888` = 0 →
connect to 0.0.0.0, silent fail) — exactly the login-era `0x03db` "connect to 0.0.0.0" bug class.

**CORRECTION: 0x36A is a KICK, not the world-enter.** `sub_1400481B0` uses `qword_140C635F0+5888`
which is a **`"logoutReason"` string** (set at line 147067) — so `sub_1400481B0` = disconnect-with-
reason. Sending `0x36A` disconnected the client ("Reason 0"). Wrong message.

## 🎉 SOLVED — THE CLIENT LOADS A WORLD (2026-08-21). Opcode 0x00AD is the world-enter.

RE of the pending-char consumer (`+376`): handler **`sub_140022480` = opcode `0x00AD` (173)**, which
runs **ONLY in char-select state 4** (right after Enter Game). It reads **6 u32s** (worldId + 5 floats)
into `+456..476`, sets up the world connection with the pending char on the **SAME connection** (`+184`,
no reconnect), and sets mgr **state 5 = LOADING**. Wire (client Read `sub_14007E9E0`):
**`0x00AD = [15 bits worldId][5 × float32]`** (5 floats = X,Y,Z + 2, the char-list vec5 pattern).

Built `WorldEntryMessages::BuildWorldEnter(worldId, x,y,z,f4,f5)` and send it first on `0x07DD`.
**LIVE RESULT: the client LEFT char-select and showed the WORLD LOADING SCREEN for world 990** (Everstar
Grove) — loading tips are Aurin/Arboria lore (correct world!), progress bar near full. The world-enter
handler `sub_140022480` fired (Frida-confirmed), and the world manager parsed the following
0x0988/0x098B/0x0981. **The clean C++ engine drove a real 16042 client from char-select into loading a
world, from scratch, NF-free.** This is the wall coming down.

## THE LAST MILE (current state)

Client is at the END of the loading screen, **streaming `0x038C` movement** (43 B, carries position
floats), waiting for the "load complete" handshake. Per the old notes this is the final-stretch stall.
To drop into the zone the server must:
1. **Answer the client's `0x038C` movement** (echo/ack) so it gets "load complete".
2. **Send the player's own entity** (`0x0262` entity-create for the char's guid, with position) + the
   minimal world stream so the client has a controllable avatar.
## LAST-MILE BREAKDOWN (careful RE needed — do NOT rush a malformed 0x0262, it crashes the client)

The loading screen drops when the client's **player entity (`a1+120`) is set** → fires `"PlayerChanged"`
(client fn @ 898014) and `"UnitCreated"`. The player entity arrives via **`0x0262` entity-create**.

**Full 0x0262 wire tree (deser.py, client Read `WS+0x96FA0`), 270 fixed bits + arrays:**
`[u32 guid][6b type][8b][5b cnt→array{5b,2b}][u32][5b cnt→array{5b}][8b cnt→array{8b}]
[7b cnt→VISUALS array{7b slot,15b displayId,14b dye,32b}][9b cnt→COMMAND array{ u32,u32,u32 (=pos
X/Y/Z floats),18b,1b,u32, cnt→array{u32,8b,8b,16b,4b,8b,…}, 8b cnt→array{u32,8b,u32}, 8b
cnt→array{16b,u32,8b,u32} }][u32][14b][14b][u32][u64][2b]`. Position = the 3 u32 floats in the COMMAND
sub-block (matches the prior "3×float32 @ bit 289" finding).

**OPEN QUESTIONS (resolve by RE before building, to avoid crashing the client):**
1. **The 6-bit `type`** value for a player unit (entity-type enum) — unknown; must find it.
2. **The player GUID / self-designation.** The client is ALREADY streaming `0x038C` movement with guid
   **`0x097998A0`** (its provisional player unit) — so the client made a local player from the `0x00AD`
   data. Unknown whether: (a) the server must send a "your unit id is X" message and then `0x0262` for
   X, or (b) the client's provisional guid must be matched, or (c) `0x0262` carries a self flag. This is
   the crux — the client only fires `PlayerChanged` when it recognizes an entity as ITS player.
3. Whether the load also needs the server to **answer `0x038C`** (world-entry.md hint) and/or a small
   "world ready" marker (`0x00FE` @ seq pos 11, or `0x0987`).

**METHOD:** RE the client's entity-create HANDLER (not just Read) — how it decides an entity is the
player (matches guid at `+120`?), and find the self/guid-assign message. Then build `0x0262` for the
player with the right type+guid+position, send after the world stream, test watching for crashes
(process alive + screenshot), revert on any instability. THEN generalize (DB provider for worldId/pos;
retarget world 1537 tutorial).

**STATE:** the engine sends `0x00AD` (world 990, Peryanna hardcoded) on `0x07DD` → client LOADS the
world (proven). Stuck at the loading screen's last step pending the player-entity/self resolution above.

## PROGRESS 2 (2026-08-21, instrumented per operator directive — NO guessing)

Built `world-oracle.py` (Frida): hooks the world-msg Read fns (0x0262/0x0981/0x0988/0x098B), the
entity-create handler `sub_14047DCF0` (WS+0x47DCF0, fires "UnitCreated"), the world-load-complete
`sub_1403B6DE0` (WS+0x3B6DE0, fires "ChangeWorld"), and the Lua event-fire. Findings, all FACTS:

1. **Target = world 1537 (arkship tutorial)** — retargeted (operator: not Everstar); confirmed by the
   arkship "OFF WORLD" loading art. `0x00AD` carries worldId; any zone is a value.
2. **Player guid = u64 @ offset 7 of the 43-byte `0x038C` movement** (the 7-11B ones are handshake).
   Read live and echoed. This session's guid = `0x97998A0` (session-specific).
3. **THE COMPLETION: `0x00AD` must be sent TWICE.** The 1st `0x00AD` → char-select mgr
   (`sub_140022480`, state 4→5, loads map). A **2nd `0x00AD`, sent once the client streams real
   movement**, reaches the WORLD dispatcher (case 0xAD @ line 943707 → `sub_1403B6DE0`) → fires
   **`ChangeWorld`** = world-load COMPLETE (loading bar fills, "continue" arrow appears).
   Frida-confirmed: `WORLD-LOAD-COMPLETE FIRED` + `LUA-EVENT ChangeWorld`.
4. **`0x0262` player entity: PARSED but not CREATED.** `READ 0x0262` fires (my 34-byte minimal entity
   is structurally valid — client doesn't crash), but `ENTITY-CREATE`(`sub_14047DCF0`) and
   `PlayerChanged`/`UnitCreated` do NOT fire. So the client parses my entity but drops it — the unit
   is never created, so the loading screen won't drop.

**NEXT (last step):** find why the parsed `0x0262` doesn't become a unit. Suspects (instrument, don't
guess): the **6-bit type=0 is wrong** (need the player/creature type value); the entity needs a valid
**position** (cmdCount 0 = no position — build the command sub-block); or the entity must arrive/route
differently. Hook the callers of `sub_14047DCF0` and the 0x0262 post-Read dispatch to see the reject.
Everything ELSE works: char-select→load→ChangeWorld complete. The client is one valid player unit away
from standing in the arkship.

**Current engine flow (world_handshake.cpp):** on 0x07DD → 0x00AD(1537) + empty 0x0988/098B/0981; on
first 43-byte 0x038C → 0x0262 player entity (guid from movement, type 0) + 0x00AD again (ChangeWorld).

## OPERATOR CALIBRATION (2026-08-21): loading bar full / ChangeWorld firing ≠ IN WORLD.
The only success is the character STANDING in the arkship. `ChangeWorld` and a full loading bar are
mechanism checkpoints, NOT entry. Do not over-claim on them.

## HONEST STATE (measured, not inferred)
- **SOLID:** char-select → client LOADS world 1537 (arkship). Reproducible. `0x00AD` = the world-enter
  (char-select mgr sub_140022480, state 4→5). A 2nd `0x00AD` reaches the world dispatcher
  (`sub_1403EC6A0`, guard chan `0x20001` MATCH) → `sub_1403B6DE0`/`ChangeWorld`.
- **NOT DONE:** actually standing in the world. Blockers, all Frida-measured:
  1. My `0x0262` player entity is PARSED (`READ 0x96FA0` fires) but **never reaches the world
     dispatcher `sub_1403EC6A0`** (no `WORLD-DISP a3=0x262`), whether sent before or after the 2nd
     `0x00AD`. So no unit is created (`sub_1403D9760`/`ENTITY-CREATE`/`UnitCreated` never fire).
     Hypothesis: during loading the client routes entity-creates into a STREAMING BUFFER that only
     replays when the world is TRULY ready — which it isn't.
  2. **Likely root cause: the world stream is EMPTY.** `0x0981` (world-init) sent with 0 ids, `0x098B`
     empty. The client probably needs real world-init data (the observed capture had 251 ids in 0x0981,
     116 × 0x098B zone blobs) to finish initializing the world; without it, loading never truly
     completes and the entity queue is never replayed.
- **Player guid:** u64 @ offset 7 of the 43-byte `0x038C` (this session `0x97998A0`); the entity-exists
  check returns "new" for it. The `0x0262` entity structure is client-valid (parses, no crash).

## PROGRESS 3 (2026-08-21, deep instrumentation — the full completion PATH mapped)

Built up `world-oracle.py` (Frida) hooking: world Read fns, world dispatcher `sub_1403EC6A0` (all
opcodes+channel), entity-create `sub_1403D9760`, entity-exists `sub_1403D90D0`, ChangeWorld
`sub_1403B6DE0`, set-player `sub_1403B5AD0`, and the Lua event-fire (arg read from **r9**, not r8).

**THE COMPLETION PATH (all client-derived, NF-free):**
1. `0x07DD` EnterGame → `0x00AD` (world-enter, char-select mgr `sub_140022480`, state 4→5, loads map).
2. Client streams `0x038C` movement; the **43-byte** ones carry the pending **player guid @ offset 7
   (u64)**. (7-11B ones are handshake.)
3. **THE PLAYER-SET MESSAGE = `0x019B` (411)**: `[u32 playerGuid][u32]`. Handler `sub_1403B5AD0`
   **looks up the entity by guid** (`sub_1403D90D0`) and sets it as the local player (`a1+120`) → fires
   **`PlayerChanged`** = entry complete. **If the entity doesn't exist, it errors: on-screen
   "Failure handling network message 'Invalid or foreign Message Id #411'"** (operator spotted this —
   it's the exact symptom: `0x019B` sent before the entity exists).

**THE WALL: the player entity is never created.** `0x0262` entity-create is PARSED (`READ WS+0x96FA0`
fires, my 34-byte minimal entity is client-valid, no crash) but **never reaches the active world
dispatcher `sub_1403EC6A0`** (only `0x00AD`/`0x019B` reach it, on channel `0x20001`). Entity-creates
route to a SEPARATE entity/grid manager that BUFFERS during world load. The entity/grid path is
`sub_1405CEF50` (internal grid command **0x12** = create) → `sub_1405CAF20` (batch) → `sub_1403D9760`.
This grid system only processes entities once the world's grid is "ready" — which my empty world stream
never achieves. The world-loading screen is NATIVE (NetworkStatus only covers login: "Authenticating"…
"Retrieving Characters"; no world-load status), so the gate is not NetworkStatus.

**Forcing `ChangeWorld`** (a 2nd `0x00AD` reaching `sub_1403EC6A0` → `sub_1403B6DE0`) fires the event and
fills the bar but does NOT truly enter (operator's calibration: bar full ≠ in world). Real gate unmet.

**NEXT — two fronts (fresh, focused effort):**
- **(A) The message router / opcode→manager map.** Find WHY `0x0262` routes to the buffering grid
  manager while `0x019B`/`0x00AD` reach `sub_1403EC6A0` (chan 0x20001). The router is upstream of the
  vtable `OnMessage` calls (pump `sub_140331990`). If the player entity can be delivered on the active
  channel (or the grid activated), `0x019B` then succeeds.
- **(B) The grid-ready trigger.** What flushes the entity/grid buffer / makes `sub_1405CAF20` process
  my `0x0262`. Likely needs the real world-init/terrain "ready" — reproduce the world-init (`0x0981`
  id list, `0x0988`, `0x098B`) from the CLIENT's own world 1537 tables (NF-free), not the capture.
- Then: entity created → `0x019B` sets player → `PlayerChanged` → standing in the arkship. THEN
  generalize (DB provider for worldId/pos) + retarget confirmed already works (1537 loads).

**Durable wins to NOT re-derive:** 0x00AD world-enter; 0x019B set-player (+its #411 error meaning);
player guid @ 0x038C[7]; the entity/grid call chain; the dispatcher channel guard (943671); the oracle
Frida tool. Engine currently: 0x07DD→0x00AD; on 43-byte 0x038C→0x00AD(2nd)+0x0262 (0x019B held back to
avoid #411 until the entity exists).

## Tools/state
- Engine: `cpp/build/Release/nexus_realm.exe` (rebuilt with world_entry). Log `we-test.log`.
- Frida (17.17): `deep-trace.py` (decrypt+router+conn), `dispatch-trace.py` (0x20EA0+0x370D0),
  `event-trace.py` (Lua events), `deser.py`, `probe-all.json` — all in `<scratch>`.
- Client: `clients/Wildstar/Client64/WildStar64.exe`; launch via `wslaunch.ps1` (cmdline must begin
  `/auth`), login `wslogin.ps1`, click `wsclick.ps1 x y`, screenshot `ws-shot.ps1`.
- Peryanna (id 32) worldId set to 990 for testing (retarget to 1537 once entry works).

## UPDATE (10:04) — entity kind 20 = Player; +272 component root cause

Set-player (0x019B, client sub_1403B5AD0) was failing at line 897985: `*(entity+272)` NULL.
Root cause: my 0x0262 entity used **kind 1** (generic, no controllable-player component). The
client's type-NAMES table gives **kind 20 = "Player"**, and the kind→sub-reader table
`funcs_14009702A[20] = sub_1400962D0` reads the full character-data block.

Decoded sub_1400962D0 COMPLETELY (WildStar64.exe.c line 241269–241354), 218 bits:
  u64 · 14b · wstr(name) · 5b · 5b · 2b · u64 · 8b count_a[4B] · wstr · 4b · 5b count_b[8B]
  · 6b count_c[4B] · 3b · 8b · 14b
Rebuilt `BuildPlayerEntity` as kind 20 with this block (all strings/arrays empty), rebuilt
nexus_realm clean. TEST PENDING: does the entity now build +272 → set-player succeed → PlayerChanged.

## UPDATE (10:13) — the error number IS the rejected opcode; 0x636 is the real set-player

TEST 1 (kind-20 entity) result via oracle: WORLD-LOAD-COMPLETE (ChangeWorld) FIRED, world 1537
loaded (loading bar full, "OFF WORLD" arkship art). 0x0262 Read RETURN=0 (kind-20 Player block
parsed clean). BUT lookup(guid) -> NULL and expectedPlayer(+25728)=0; client showed
"#411 invalid or foreign Message Id".

OPERATOR CLUE (decisive): the error was previously #610, now #411. **The number = the rejected
opcode in DECIMAL**: 610=0x262 (old, entity-create rejected), 411=0x19B (now, my set-player). So
kind-20 fixed the entity (0x262 accepted); the wall moved to set-player. 0x19B (sub_1403B5AD0)
fails its handler (+272 null) and the framework logs "invalid/foreign #<opcode>".

FIX: the correct world-channel set-player is **opcode 0x636** (client dispatch case 0x636 in the
SAME world dispatcher sub_1403EC6A0 -> sub_14057A630). It has the expectedPlayer FALLBACK: if
entity[guid] missing, it stores guid at +25728 so the next 0x0262 auto-binds (sub_1403D9760 line
928340) and fires PlayerChanged. Wire (Read sub_1400B09D0): [32b unitId][1b flag][32b guid].
Requires container a1+25744 non-null (set by ChangeWorld). New flow: 2nd 0x00AD -> 0x636 -> 0x0262.
Built. TEST 2 pending.

## BREAKTHROUGH (10:55) — the +272 blocker is ONE zeroed faction field

Full bidirectional tap (nettap.py client-side + server.log) proved: only 3 world messages
(0xAD, 0x262, 0x19B); entity CONSTRUCTS ok (UnitCreated fires, in lookup map) but entity+272
(the unit component set-player 0x19B requires) stays NULL; client asks for nothing -> not a
missing message.

Root cause found in client construction: common tail calls
  sub_14045AC60(entity, faction2 @ struct+216)
  -> if faction2 != 0 && registry qword_140C665D0: sub_140716FA0(entity, faction2, &entity+272)
     installs entity+272 (hashtable lookup keyed by the faction id; fails if 0/invalid).
The entity's two 14-bit fields at struct+212 / struct+216 are Faction1 / Faction2. I was sending
BOTH as 0, so the +272 installer skipped -> set-player E_FAIL -> #411.

FIX: BuildPlayerEntity now writes Faction1=Faction2=166 (Exiles Player faction, parent 165;
client faction2.tsv). Rebuilt. TEST PENDING. If +272 installs, 0x19B set-player should bind the
player, set container+player, fire PlayerChanged, drop the loading screen.

Deadlock note (why 0x19B, not 0x636): container(+25744) is zeroed by ChangeWorld; only a bound
player sets it; 0x636 needs a live container; 0x19B is the only bootstrap and it needs entity+272.
So installing +272 (this fix) is the single unlock.

## SUCCESS (11:05) — faction fix works: entity+272 installed, set-player OK, PlayerChanged FIRED

TEST result (nettap): CONSTRUCT ret=0, **entity+272=0x1db4d871240 (NON-NULL!)** -- faction 166 at
struct+216 triggered sub_14045AC60 -> sub_140716FA0 which installed the unit component. Then
W-DISP 0x19b -> **LUA PlayerChanged** -> SET-PLAYER ret=0 (was -2147467259 #411). First time the
player has EVER bound. #411 gone; loading screen fully lit (not grayed).

REMAINING GATE: loading screen holds at 100% (does NOT drop into 3D world within ~10s -> operator
says that means not fully loaded). New signal: right after PlayerChanged the client starts sending
a NEW outbound container **0x0244** (55B, encrypted uplink) every movement tick -- it's now
streaming player movement (thinks it's logically in-world), but the loading-screen UI gate hasn't
released. Next: find what dismisses the load screen (world-ready condition) / whether 0x0244 needs
a response / whether the spawn position (1437.82,85.53,-106.82 world 1537) is valid terrain.
