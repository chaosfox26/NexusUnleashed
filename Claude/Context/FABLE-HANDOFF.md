# Handoff — what we're doing right now (for Fable)

_Written 2026-08-20, mid-investigation. Self-contained. Read this, then `build-notes.md`._

## The one-line mission
Get the **real WildStar 16042 client** to log in and stand **in the world** against our
**clean-room C++ engine** (`cpp/`), owing NexusForever **nothing**. Everything is derived
from the CLIENT itself (its binary, tables, Lua) and our own data. **HARD RULE: no NF —
no NF source, servers, captures, or protocol docs, ever.** The client defines the protocol
facts; we read them off the client.

## Where the client is stuck (the current barrier)
After STS login (works end-to-end) the client connects to our realm server on **23115**,
sends realm-enter, and sits on the Login screen showing **"Network Status: Retrieving
Account Information."** It never advances to RealmSelect → Character. We are trying to clear
that overlay and reach the character screen (character *creation* is one step past that).

## The breakthrough this session (what we just learned, in order)
1. The account/realm data all rides the **single 23115 connection** (client opens only
   23115 + STS 6600 — verified by netstat; there is NO separate auth server).
2. We reversed the two server→client messages the client's account state waits for, from the
   client's OWN deserializers, and **machine-verified** them with `deser.py`:
   - **`0x7A1`** account data (Read `WS+0xA2110`) → advances account state 1→2.
   - **`0x761`** realm list (Read `WS+0xAC9D0`) → fires Lua `RealmListChanged` + clears the
     overlay = the ADVANCE. Full wire map: `spec/protocol/realm-list-0x761-and-account-0x7A1.md`.
3. We sent those, and the client's own `0x761` Read returned **eax=0 (success)** — so our
   BYTE FORMAT IS CORRECT. But instrumentation (Frida hooks on the client) showed the
   account-state dispatcher (`WS+0x45A70`) was **never called** and the state **never armed**
   (`WS+0x45D13` state→1 never ran). The connection state machine climbed **3→5→6 then
   stalled**.
4. **Root cause (found via a raw client-socket trace on OUR server side):** the client's
   realm-channel frames are **encrypted `0x0244` containers** (`[u32 size][u16 0x0244]
   [encrypted [u16 innerOp][body]]`). We had been REPLYING WITH CLEAR (unencrypted) frames.
   The client parses a clear frame's opcode (so Read runs) but **never dispatches it to the
   connection's active state**, so the account state never wakes up and our data is dropped.
   It's a **framing/envelope mismatch, not a content bug.**
   - Realm channel: client→server container op = **`0x0244`**; the client's message router
     special-cases **`0x03DC`** for server→client. (World channel 24000 uses the same pair.)

## The fix under test right now
Switched our three realm-channel pushes (`0x7A1`, `0x761`, `0x0117` char list) from
`SendClearGameMessage` → **`SendGameMessage`** (which wraps in the encrypted **`0x03DC`**
container via `WorldPacket::EncodeServer`, `EncryptForClient`, same PacketCrypt/seed we
already use to DECODE the client's inbound `0x0244` containers). Files:
`cpp/src/realm/world_handshake.cpp`. Server rebuilt; **testing in progress.**
> NOTE: this overturns the earlier note "realm S→C is CLEAR framing" — that was wrong; it
> made Reads run but never dispatched. Containers are the envelope both directions.

## How to test (the loop we're using)
1. Build: VS18 cmake at
   `"C:\Program Files\Microsoft Visual Studio\18\Community\...\CMake\bin\cmake.exe" --build cpp/build --config Release --target nexus_realm`
   (server exe locks itself while running — stop it first).
2. Run server from `cpp/build/Release/` (needs `realm.json` beside it, gitignored).
3. Client is at `C:\Games\Wildstar\Client64\WildStar64.exe`. To force a fresh realm
   handshake: restart the server (client drops to "Connection Closed"), then log in.
4. Drive login with `<scratch>/wslogin.ps1` (test creds; clicks fields + Log In).
5. Watch: the **server log** (`realm-run.log`) for the `[RAW IN]` frames + our sends, and a
   Frida **event-trace** for the Lua events. **The win = `RealmListChanged` fires** and the
   overlay clears. Screenshot the client window ONLY with `<scratch>/ws-shot.ps1`
   (never full screen — privacy rule).

## The RE toolbox (all in `<scratch>/`, client-only, read-only)
- `deser.py <ReadAddr…>` — recursive decoder of the client's message Read functions →
  full field tree + bit widths (how we verified `0x7A1`/`0x761`). Reusable for any message.
- `event-trace.py` — hooks `WS+0xEA3E0` (the client's Lua event-fire fn; event name in rdx)
  → logs every client event. `RealmListChanged`/`NetworkStatus` are the signals.
- `sock-trace.py` — hooks ws2_32 send/recv → raw realm-channel bytes (how we found the
  `0x0244` container). `conn-trace.py` — hooks the connection/account state-write sites to
  see where the handshake stalls. `diag-dispatch.py` — hooks the account dispatcher/handlers.
- `wsdis.py` / `xref.py` / `xref-addr.py` — static disasm + xref of the client
  (`C:\Games\Wildstar\Client64\WildStar64.exe`, base 0x140000000).
- `probe-all.json` — the client's full registered opcode table (1121 ops, sizes, fn ptrs).
- Frida gets flaky after many attach/detach cycles — kill all python hooks and relaunch ONE
  if a hook goes silent.

## Key client addresses (RVA-ish; base 0x140000000)
- Lua event-fire `WS+0xEA3E0` · message pump `WS+0x331990` · factory `WS+0x330910`
- account/realm state dispatcher `WS+0x45A70`; arm (state→1, sends via mgr) `WS+0x45D13`;
  `0x761` handler `WS+0x45E30` (fires `RealmListChanged`+`NetworkStatus`); `0x7A1` handler
  `WS+0x461E0` (state→2). Realm entry Read `WS+0xac130`; address sub-struct `WS+0xabc00`.
- Bit primitives: `WS+0x6c090` read N bits; `WS+0x6bf60`/`WS+0x6be30` 16/8-bit; `WS+0x6c120`
  u64; `WS+0xa80f0` u32; `WS+0x337160` N bytes; `WS+0x336a40` wide string.

## If the container fix works
RealmListChanged fires → RealmSelect shows our realm → player selects → character retrieval
(the already-validated `0x0117` char list is consumed by the char-select state) → character
screen. OPEN question then: does SelectRealm reconnect to the realm entry's address
(host `+0x40`/port `+0x48`) or reuse the socket — decides if those fields must point at us.

## Rules (unchanged)
No NF (above). Never brute-force opcodes (malformed bodies crash the live client — always
match the client's own deserializer). Never commit personal data — run
`provenance/privacy-guard.py` (scans tracked files); sensitive specifics live only in the
gitignored `Claude/Context/local-notes.md`. Screenshots are WildStar-window-only.
