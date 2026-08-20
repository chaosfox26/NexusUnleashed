# The account-retrieval barrier ("Retrieving Account Information") — RE state

> **UPDATE 2026-08-20 (evening) — TWO decisive findings, one blocker left.**
> 1. **The launch-arg theory is DISPROVEN.** The operator's WORKING client (reaches world on
>    the NF realm) has the IDENTICAL command line — `/auth localhost /authNc localhost /lang en
>    /patcher localhost /SettingsKey WildStar /realmDataCenterId 9`, **no `/ncProgramId`**. So
>    the `+0x170c` gate (set from the `ncProgramId` arg) is a side path, NOT the real advance.
>    It's a server-message difference. (`WildStar64.exe` also cannot be launched directly — it
>    exits; it needs its launcher `Wildstar.exe`/`NexusUnleashed Launcher.exe` as parent. The
>    launcher points the client at OUR ports 6600/23115.)
> 2. **THE REALM-HELLO RESPONSE IS OPCODE `0x0591`** (4 bytes: one u32, bit0 = a flag;
>    Read `WS+0x7AB50`). The connection's opcode dispatcher is vtable slot 9 = **`WS+0x370D0`**
>    (checks state `[conn+0xa8]`, channel `edx==[conn+0xe8]+0x98`, switches on opcode). It
>    handles `0x3`, `0x3db`(987), **`0x591`**(1425), `0x63d`(1597). Receiving `0x591` while the
>    connection is in **state 6 or 8** (exactly where it parks after sending its `0x0592`
>    realm-hello) advances it to **state 9** (`mov [conn+0xa8], 9`) and stores `body[0]&1` to
>    the manager. This is the server response the client waits for — matches the working-client
>    evidence (it's a message, not a launch arg).
> 3. **BLOCKER: connection messages must arrive in a `0x03DC` container, and our S→C container
>    encryption is REJECTED by the client.** The router special-cases `0x3`/`0x76`/`0x3dc` →
>    connection dispatcher; a normal clear opcode (our `0x591`) goes to the general/account
>    path instead and never reaches `WS+0x370D0` (confirmed live: only `0x3` arrives there).
>    Sending `0x591` as our `0x03DC` container ALSO fails to arrive — the client can't decrypt
>    it. Our PacketCrypt is self-consistent (Decrypt/EncryptForClient are proper CFB inverses)
>    and DECODES the client's C→S `0x0244` container correctly with `AuthChannelKey`
>    (0xD283F5B34A8DC685). So **S→C uses a DIFFERENT key than C→S** (build-const for C→S;
>    session/handshake-derived for S→C, like the world channel re-keys). **NEXT: find the S→C
>    key derivation** (hook the client's `0x03DC` decrypt at `[msgMgr+0x1628]` to read its key/
>    register, or derive from the `0x0003` hello / `0x0592` handshake / SRP session key). Once
>    S→C encryption is right: send `0x591` (container) → conn state 9 → account state arms →
>    our already-correct `0x7A1`/`0x761`/`0x0117` get consumed → realm select → character.
> Tools added this session: `route-trace.py`, `conndisp.py`, `gate2.py` (all `<scratch>/`).

> **UPDATE 2026-08-20 (late) — THE S→C ENCRYPTION BUG IS DIAGNOSED. Seed is correct; the
> cipher must be a CONTINUOUS STATEFUL STREAM.**
> - **The realm-channel cipher seed the client uses = `0xD283F5B34A8DC685` — EXACTLY our
>   `AuthChannelKey`.** Read live from the client's cipher setup (`WS+0x2a050`, seed in r8;
>   the setup delegates key-expansion to `WS+0xC2EB0`). The seed is DERIVED at runtime: a
>   multiplicative hash (init `kSeedInitial` 0x718DA9074F2DEB91, mult **`0xAA7F8EA9`**) over 16
>   bytes at `manager+0x1638` combined with `manager+0x1630`, ×mult (builders `WS+0x38240`
>   conn / `WS+0x46450` acct, both also send the C→S `0x244` container). For our fixed game
>   token that hash yields `0xD283…`, which is why our hardcoded constant matched.
> - **The client sets up the cipher ONCE per connection** (single `WS+0x2a050` call) and uses
>   it as a **continuous stream** — the register keeps evolving across every message.
> - **OUR BUG:** `PacketCrypt` methods are `const` and re-init the feedback register from the
>   fixed seed on EVERY message (stateless per-message). So we decode the client's FIRST
>   container (`0x0592`) correctly (register still at initial), but our reply (`0x0591`) is
>   encrypted from the INITIAL register while the client decrypts it from the register state
>   ADVANCED by having encrypted `0x0592`. Mismatch → client can't decrypt → drops it.
> - **THE FIX:** make `PacketCrypt` STATEFUL — one instance per session (already the case:
>   `GameSession::crypt`), register persists and advances through every `Decrypt` (inbound)
>   and `EncryptForClient` (outbound) as ONE shared duplex stream. Then: decode the client's
>   `0x0592` (advances register) → encrypt `0x0591` from the advanced register → client
>   decrypts it → conn state 9 → account state arms → our `0x7A1`/`0x761`/`0x0117` consumed →
>   realm select → character. **Get the exact register-carry from the client's cipher process
>   method** (the cipher object at rcx from `WS+0x2a050`; find its process/XOR method and read
>   how it stores the evolved register back) — or derive empirically with the client as oracle
>   (persist final feedback as the u64 register; verify `0x591 READ` runs). Tool: `cipherseed.py`.


> **UPDATE 2026-08-20 — SOLVED ON PAPER, IMPLEMENTED, STAGED (awaiting one live test).**
> The account state dispatcher (`WS+0x45A70`) and its state machine are fully mapped, and the
> two server pushes it waits for are **reversed from the client's own deserializers and
> machine-verified** (`deser.py`): `0x7A1` account data (Read `WS+0xA2110`, advances state
> 1->2) and `0x761` realm list (Read `WS+0xAC9D0`, fires `RealmListChanged` + clears the
> overlay = the ADVANCE). Full wire map: `spec/protocol/realm-list-0x761-and-account-0x7A1.md`.
> **C++ serializers built + unit-tested** (`cpp/src/proto/account_realm.*`, sizes verified:
> 0x7A1 = 34 B, empty 0x761 = 16 B) and **wired to push on realm-enter** before the char list
> (`world_handshake.cpp`, toggles `SendAccountData`/`SendRealmList`/`IncludeRealm`). Next: a
> LIVE test — relaunch client, log in, watch for the overlay clearing + `RealmListChanged`
> (event-trace `WS+0xEA3E0`). Open: does SelectRealm reconnect to the entry's address (host
> `+0x40`/`+0x48`) or reuse the socket — decides whether the address fields must point at us.

> **UPDATE 2026-08-20 (live testing) — the real blocker is the CONNECTION handshake, not the
> account messages.** Established live (client + our C++ server, all localhost):
> - Our `0x7A1`/`0x761` wire formats are correct — the client's own `0x761` Read returns
>   **eax=0** on a clear frame. The realm-channel envelope is the **encrypted `0x0244`
>   container** C→S; the client's router special-cases **`0x03DC`** S→C (world chan uses the
>   same pair). Raw server trace confirmed the client's frames: `[u32 size][u16 0x0244]
>   [encrypted [u16 inner][body]]` (inner `0x0592` decoded cleanly by our server).
> - **The account-state machine (dispatcher `WS+0x45A70`) never activates.** Frida hooks:
>   `0x45D13` (state→1 arm), `0x45A70` (dispatcher), `0x461E0`/`0x45E30` (0x7A1/0x761
>   handlers) — **none run**. Our messages are received (router logged 3× `0x3dc`) but
>   dropped: nothing is armed to consume them.
> - **The CONNECTION state machine (funcs `WS+0x37xxx`–`WS+0x3Axxx`, state at `[obj+0xa8]`)
>   parks at state 6** after sending its realm-hello `0x0592` (built by `WS+0x3A665`, stamps
>   build **0x3EAA=16042** + a hardware survey, sent as a `0x0244` container). Live conn-trace:
>   states **3 → 5 → 6, then stall.**
> - The advance decision is `WS+0x386A4`: go to **state 7 (ADVANCE)** iff gate
>   `[mgrObj+0x170c] == 1` AND `[conn+0xe8] == 0`; else **state 3 (WAIT)**. Live: it takes
>   the **WAIT** path. `[+0x170c]` is set from a **console/config-var read** (`WS+0xB9B1`,
>   via `WS+0x1a4720`), not a simple constant — provenance still being pinned.
> - **OPEN = the server's response to `0x0592`** that completes the connection handshake and
>   flips the gate, BEFORE account data will be consumed. Encryption of our S→C `0x03DC`
>   container not yet confirmed accepted (hook the `0x03DC` handler `WS+0x1523C` to read the
>   decrypted inner opcode = the decisive next test). Tools added: `deser.py`, `route-trace.py`,
>   `conn-trace.py`, `gate-trace.py`, `sock-trace.py`, `diag-dispatch.py` (all `<scratch>/`).

**Status (2026-08-19): the single blocker between the C++ engine (proven at parity) and
the character screen / world.** All findings client-only (WildStar64.exe, base
0x140000000), NO NF. Addresses are RVAs (`WS+` = base-relative).

## What the client is doing (measured, not assumed)

- After STS login + realm-enter (inner op **0x0592**, 396B, carries a hardware survey),
  the client is on the **Login screen** (`PreGame/Login/Login.lua`) showing the overlay
  **"Network Status: Retrieving Account Information"** (confirmed by a window-only
  screenshot — it is NOT RealmSelect or Character yet, so the char-select object G being
  null is EXPECTED). It is blocked in **post-login account retrieval**.
- It fires exactly one meaningful Lua event and then waits: **`NetworkStatus`** (sets the
  overlay). No `RealmListChanged`, no `CharacterList`, no screen change. It is waiting for
  the server to push the **account data** that completes retrieval and advances the
  pre-game state.
- Our C++ server sends the hello + the (validated) `0x0117` char list on realm-enter. The
  client **DOES process the char list** (its Read `WS+0x7FAB0` runs to success and the
  message is dispatched — see the msg2 call tree), but it does **not** fire `CharacterList`
  (`WS+0x21A4C`, which needs G) and does not advance. So the char list is (correctly)
  premature — the client wants the **account-retrieval message(s) first.**

## Key RE assets discovered (the enablers for the next push)

- **`WS+0xEA3E0` = the Lua event-fire function** (event name in `rdx`, args in `r8`/`r9`).
  Hooking it logs EVERY client event by name → you see exactly what the client does and
  waits for. This is the single most useful observation hook. (tool: `event-trace.py`)
- **Message pump = `WS+0x331990`**; **factory (opcode→descriptor) = `msgMgr->vtable[0x130]`
  = `WS+0x330910`**. Descriptor: `+0x00` opcode, `+0x08` size, `+0x10/+0x18/+0x20` fn ptrs
  (Read/Write/variants — all just SERIALIZATION; the semantic handler is dispatched
  separately by the active pre-game state, so descriptor fn18/fn20 do NOT reference
  account/realm strings). (tool: `probe-all.py` → `probe-all.json` = all 1121 registered
  opcodes with sizes + fn ptrs.)
- Realm-enter (0x0592) is built by `WS+0x14003A665` and sent from the hello handler; its
  Write is `WS+0x7DCD0`. The hello handler's full call tree (tool: `pump-stalk.py`) sends
  0x0592 and fires the `NetworkStatus` event.
- Char-select state **G = `*[0x140C66DA8]`** (null until char-select); ctor `WS+0x140020730`
  (6 vtable-dispatched pre-game-state callers, e.g. `WS+0x140046340`). Event-firers:
  `RealmListChanged` `WS+0x140046094`, `CharacterList` `WS+0x21A4C`.

## The precise open question

**What message (opcode + body) does the client expect after 0x0592 to complete
"Retrieving Account Information" and advance the pre-game state?** (entitlements /
realm-list / a realm-enter response). It is a server PUSH, dispatched to the ACTIVE
pre-game state's message handler (the equivalent of the char-select dispatcher
`WS+0x20EA0`, but for the Login/account state).

## Best next approaches (client-only, no NF)

1. **Find the active pre-game (Login/account) state object + its message handler** (its
   vtable's message-dispatch slot, like char-select's slot 11 = `WS+0x20EA0`). Enumerate
   the opcodes that dispatcher handles → those are the expected account messages. The
   state machine's current-state pointer is a global distinct from G.
2. **Hook `WS+0xEA3E0` (event-trace) + inject candidate messages** (the engine's injector,
   §build-notes) and watch for a NEW event (`RealmListChanged` / a screen change). CAUTION:
   only inject messages with correct-length bodies — malformed zero-body messages have
   crashed the live client before; never brute-force blindly against the operator's client.
3. Diff the hello (msg1) vs char-list (msg2) pump call trees to isolate the message
   dispatch from frame-render noise (both trees share a large render tail on the main
   thread).

## Tools (in the session scratchpad; recreate from this doc if lost)

`event-trace.py` (hook 0xEA3E0), `probe-all.py` (mgr capture + full factory enum),
`pump-stalk.py` (per-message call-tree via Stalker), `analyze-handlers.py` (parallel
handler-string analysis), `ws-shot.ps1` (WildStar-window-only screenshot — never the whole
screen), plus static tools `wsdis.py`/`xref.py`/`xref-addr.py`/`read-g.py`/`factory-call.py`.
The engine carries a **message injector** (`cpp/src/realm/world_handshake.cpp` reads
`inject.txt`) for safe candidate probing without rebuilds.

## 2026-08-20 — stateful cipher attempt 1 (NOT yet working; definitive next step below)

`PacketCrypt` made STATEFUL: one instance per session (`GameSession::crypt`), persistent
feedback `stream_[8]` carries across messages; `Decrypt`/`EncryptForClient` now non-const and
mutate it (world_packet/game_server take non-const `PacketCrypt&`; unit tests moved to the
two-endpoint continuous-stream model — ALL GREEN). Built + tested LIVE: the client's `0x0592`
still decrypts and our clear `0x7A1` still Reads (eax=0), **but our container `0x591` still does
NOT decrypt on the client.** So a single shared-`fb` carry is not the exact model.

Open nuance: the seed-HASH uses mult `0xAA7F8EA9`; our key-EXPANSION uses `0xAA7B6B29` (our C→S
decrypt works, so the key table is right). What is NOT matched is the **per-message state
carry**: does `fb[8]` persist while the block-counter resets per message? do BOTH persist?
separate C→S vs S→C streams? (Only ONE `WS+0x2a050` cipher setup was observed = one cipher.)

**DEFINITIVE NEXT STEP: reverse the client's cipher PROCESS/XOR method** (sibling of the
key-expansion `WS+0xC2EB0`; cipher object from `WS+0x2a050`) to read exactly what state it
stores back after each message, then mirror it 1:1. Hook it during the client's own `0x0592`
ENCRYPT to capture the register before/after. `0x591` itself is confirmed correct (4-byte u32);
only its S→C container encryption is wrong. Tools: `cipherseed.py`, `conndisp.py`, `r591.py`.

## 2026-08-20 — THE CIPHER IS SOLVED (validated byte-exact). New narrower blocker: S->C container decrypt.

**Root cause of every "container won't decrypt": our PacketCrypt was the WRONG ALGORITHM.**
The realm/world cipher is a **qword-wise CFB** (not byte-wise). Reversed 1:1 from the client
(WS+0xC2EB0 key expansion, WS+0xC2BD0 ctor, WS+0xC2D10 encrypt, WS+0xC2DE0 decrypt) AND
cross-checked against the client DECOMPILATION at
`Project Resources/_Client-RE/ChargeIn-WildstarClientIDA/IDA/functions/sub_1400C2D10.c`:
- key table: 16 qwords. `key[0]=(kSeedInitial+seed)*0xAA7F8EA9`; `key[i]=(key[i-1]+seed)*0xAA7F8EA9`.
- **register (initial feedback) is FOLDED from the key table:** `reg=kSeedInitial(0x718DA9074F2DEB91);
  for each key qword: reg=(key[i]+reg)*0xAA7F8EA9`. (This fold is the step I had missed; for
  seed 0xD283F5B34A8DC685 it yields **0x7d546d1d1994c849**, which I confirmed live.)
- process: qword CFB. counter=(uint32)(len*0xAA7F8EAA); idx=counter&0xF, counter++/block;
  out_q=key[idx]^in_q^reg; reg = encrypt?out_q:in_q. Byte-wise tail for the remaining <8 bytes.
- STATELESS per message.
**VALIDATED byte-exact**: captured the client's real 0x0592 container plaintext AND ciphertext
(via hooking WS+0xC2D10) and our cipher decrypts one to the other perfectly — it's now a
known-answer unit test (`cpp/tests/test_packet_crypt.cpp`). `cpp/src/crypto/packet_crypt.*`.

**THE DECOMPILATION IS THE ACCELERATOR (operator's pointer):**
`Project Resources/_Client-RE/ChargeIn-WildstarClientIDA/IDA/` = full Hex-Rays decompilation
(27,245 per-function .c files + WildStar64.exe.c) + `response-codes.md` + Lua API docs. USE IT
for the wire formats ahead. The message router is `sub_140014F10.c` (readable): switch on the
outer opcode — `0x76`→handler `msgMgr[708]` (msgMgr+0x1620), `0x3DC`→`msgMgr[709]` (+0x1628),
`0x3` hello, else the normal factory+dispatch chain. The container decrypt runs
`(*(handler[0x10]+0x20))(handler[0x18], data+1, *data-4)` ONLY `if (handler)`.

**NEW BLOCKER (narrower): our S->C container is received but NOT decrypted** — hooking the
client's decrypt (WS+0xC2DE0) shows it is never called for our S->C container (0x03DC OR 0x76).
So the client's S->C decrypt-cipher slot (msgMgr+0x1620/+0x1628) is null at account-retrieval
time. **NEXT: find what installs the S->C decrypt cipher** (trace which message/handshake step
sets msgMgr[708]/[709] to the cipher object) and/or the correct realm S->C container opcode —
all via the decompilation. Cipher setup callers: sub_140038120 (conn op3), sub_140046340
(acct connB op3), sub_140039380 (conn advance), sub_140037F30 (0x3db) — each calls
sub_14002A050(handler, seed_lo, seed_hi) which key-expands into handler+0x10. Everything else
(0x0591 realm-hello response, account/realm/char wire formats) is ready and correct.

### The connection handshake sequence, from the decompilation (KEY)

The connection dispatcher's opcodes (`0x3`, `0x591`, `0x3db`, `0x63d`) advance a state machine
at `[conn+0xa8]`:
- **`0x591`** (realm-hello response): state 6 -> **9** (WS+0x370D0 / sub_1400370D0).
- **`0x3db`** (`sub_140037F30.c`): **requires state 9**; reads the message into
  `global(qword_140C635F0)+5688..+5772`, then **INSTALLS the S->C decrypt cipher** via
  `sub_14002A050(conn+240, _, *(global+5680))` (seed at global+5680) and advances state 9 -> **10**,
  firing event id 516357.
So the S->C cipher only exists AFTER `0x3db` is processed at state 9 -> **all S->C messages up to
and including `0x3db` must be CLEAR (uncontainered); encrypted containers only work afterward.**
That is why our encrypted `0x0591` never decrypted — it must be sent CLEAR, and it must reach the
connection dispatcher first.

**REMAINING: clear `0x591` is Read by the client but does not reach the connection dispatcher
(WS+0x370D0) — only the special-cased `0x3` does.** So the open problem is now purely *delivery*:
how a normal clear opcode (0x591) is routed to the connection's message handler (the dispatch
chain in `sub_140014F10` iterates `a1[662]` handlers calling `(*(h+0x58))(h, channel, op, body)`;
the connection must be in that chain and the channel id must match `[conn+0xe8]+0x98`). Trace the
dispatch chain / channel id in the decompilation. Once `0x591` reaches the dispatcher: state 9,
then send clear `0x3db` (with the fields it reads + the global+5680 seed) -> S->C cipher installed
-> then the account/realm/char containers flow. The cipher + `0x591`/`0x3db` semantics are done;
this is the last wiring problem.
