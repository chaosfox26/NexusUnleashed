# Session 2026-08-20 — LOGIN CRACKED TO THE CHARACTER CREATOR

**The real 16042 client now logs into the clean-room engine and runs the entire
character creator — end to end, screenshot-proven, zero NexusForever.**

This session took the client from a 14-hour wall at *"Retrieving Account Information"*
all the way to a fully working **character creation screen** (Experience → Race → Class →
Path → Customize → Finalize). A character was built start to finish and the Finalize page
rendered with the model, the name fields, and Enter Game. Everything below was derived from
the client itself (the IDA decompile + a live Frida tracer) and our own running realm as a
behavioral oracle — **no NF source, captures, or protocol.**

## The two walls that fell today

### Wall 1 — "Retrieving Account Information" (stood 14+ hours)
The client authenticates over STS, enters the realm channel (0x0592), and then parked.
The break was the **packet cipher**: it is a **qword-wise CFB**, not byte-wise
(constants: key-mult `0xAA7F8EA9`, counter-mult `0xAA7F8EAA`, register const
`0x718DA9074F2DEB91`; the register is a *fold* of the key table — the missed step). Once
the cipher was byte-exact (known-answer test against captured ciphertext/plaintext), the
connection handshake completed: `0x0591` (conn state 6→9) then `0x03db` (9→10), both sent
in the connection envelope `0x76`.

### Wall 2 — "Connecting to realm" (the operator's insight cracked it)
After `0x03db` the client sat at "Connecting to realm." The operator's read was exactly
right: **it's dialing the realm server, and the address is zero.** Proven by hooking the
client's own `connect()` — it was dialing **`0.0.0.0:0`**. The realm address is carried in
the **first `u32` + `u16` of the `0x03db` body** (client applies `htonl`/`htons`,
`sub_140334BB0`); we were sending zeros. Fixed: `Build3db` writes `127.0.0.1` + a real port;
the server stands up a listener there.

The client then opens a **new socket** to that address (the realm connection — its own lane,
proven by the router: the container opcode only selects the cipher, the channel is per-socket).
On that connection the server sends an **encrypted `0x0003`** (in the `0x76` envelope) — which,
because the connection object is at state 10, routes to the client's **char-select handler**
`sub_140038120`, creating the account object and completing the connection (state 10→11).
A *clear* `0x0003` is dropped (wrong channel path) — the encrypt is load-bearing. Then the
server sends the **`0x0117` character list** and the character screen populates.

## Verified live (screenshots)
1. Character **select** screen — existing character listed, "Create a New Character",
   "Enter Game", realm "(PvE)".
2. Character **creator** — Experience (Novice/Veteran), then Race/Class/Path/Customize/Finalize.
3. **Finalize** — a full character built (Aurin · Esper · Soldier · Exile), model rendered,
   name fields validated, Enter Game ready.

## What's next (Phase 07 — World Entry)
Enter Game from Finalize sends **`0x5CD5`** (298 B) — the create-character request (name +
race/class/path/sex/faction + appearance). The server does not answer it yet. Making it real
is the next phase, and it is a bigger build than the login handshake:
1. Parse `0x5CD5` (its appearance/bone arrays).
2. Build + send the create-result response (opcode not yet pinned; char-select mgr channel
   `qword_140C66DA8`; pending flag at +368). Client-side flow: `sub_140023E90` sends,
   `sub_140025070` marshals the result, `sub_140024DD0` is the select-existing/enter path
   (msg 1926).
3. Persist a full character to the DB.
4. **The world server itself** — map load, entity spawn, movement. This does not exist in the
   clean engine yet; even a perfect create-response lands the client at the world-load wall.
   "Standing in the world" = building the world server = the North Star.

## Tools built this session (offline-first, per operator directive)
- `Project Resources/_Client-RE/tools/loginmap.py` — offline login state-machine mapper over
  the 27k-function decompile (states, opcodes, channels, transitions).
- One-shot Frida tracers (goal functions + `connect()` backtrace) — the backtrace is what
  proved the `0.0.0.0:0` dial. Deliberately rare-firing, not the continuous position traces
  that lag the client.

## Engine changes (cpp/)
- `crypto/packet_crypt` — the corrected qword-CFB (both directions), known-answer tested.
- `proto/account_realm.cpp` — `Build3db` now carries the realm address.
- `realm/world_handshake.cpp` — `RegisterRealmConnection`: encrypted `0x0003` + char list on
  the realm lane. `net/world_packet` + `game_server` — `EncodeServerVia` / `SendGameMessageVia`
  for explicit container opcodes; `main.cpp` — the realm-connection listener.
