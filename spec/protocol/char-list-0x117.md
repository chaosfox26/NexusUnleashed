# Character List (opcode 0x0117) — wire format

**Status: DERIVED FROM THE CLIENT'S OWN DESERIALIZER (2026-08-19). Clean provenance —
no NF source, no NF captures. Every field below was read out of WildStar64.exe's
`Read` function for this message, observed live via a read-only Frida session while the
real 16042 client parsed our own messages.**

## How the message pump works (the discovery that unlocked all message formats)

Inbound realm messages are **pre-deserialized** before the screen handlers see them.
The pump is `WS+0x331990` (`WildStar64.exe` base `0x140000000`). It:

1. reads a 32-bit field, then a **16-bit opcode** from the bitstream (bit-reader below),
2. looks up a **descriptor** via `msgMgr->vtable[0x130](opcode)` = `WS+0x330910`
   (a pure opcode→descriptor lookup; returns null → client logs `"Message Id #%d"`),
3. the descriptor drives allocation + `Read`, then dispatches to the screen handler
   (char-select handler = `WS+0x20EA0`).

**Descriptor layout** (heap object returned by the factory):
`+0x00` u32 opcode · `+0x08` u32 struct size · `+0x10`/`+0x18`/`+0x20` fn ptrs.
For **server→client** messages the deserializer (`Read`) is at **`+0x20`**.

Descriptors captured live (sandbox-called the factory — a lookup, no mutation):

| opcode | struct size | Read fn |
|---|---|---|
| **0x117 char-list** | **0x68** | **WS+0x7FAB0** |
| 0x36 | 0x04 | WS+0x7AB50 |
| 0xAD | 0x18 | WS+0x7E9E0 |
| 0xE7 | 0x10 | WS+0x7FDE0 |
| 0x33D | 0x0C | WS+0x7E970 |
| 0x03 hello | 0x38 | (fn10 WS+0xA8E00 / fn18 WS+0xA8E10 / fn20 WS+0xA9140) |
| 0x592 realm-enter (c→s) | 0x2A8 | fn10 WS+0x7DCD0 / fn18 WS+0x7DD40 |

## Bit-reader primitives (all little-endian, LSB-first — matches the SRP LE finding)

- `readBits(stream, &dst_u32, nbits)` = **`WS+0x6C090`** — reads `nbits`, stores u32.
- `readU64(stream, &dst_u64)` = **`WS+0x6C120`** — reads **64 bits**.
- fast extractor `WS+0xA71C0(streamState+0x18, nbits)` → value; slow path `WS+0x336D60`.
- block/array read `WS+0x337160(stream, dst, count*elemBytes)`; alloc `WS+0x3374E0(ctx, bytes)`.

## 0x0117 top-level struct (0x68 bytes) — read order = wire order

`Read` = `WS+0x7FAB0` (rcx=stream, r14=allocator, rbx=struct):

| off | width / reader | meaning (inferred where noted) |
|---|---|---|
| +0x00 | u64 (`0x6C120`) | header id — INFERRED (server time / realm id) |
| +0x08 | u32 (32b) | **character count N** |
| +0x10 | ptr → N × 0xA0 | **character records** (per-char reader `WS+0x7F720`) |
| +0x18 | u32 (32b) | count2 |
| +0x20 | ptr → count2 × u32 | u32 array (block read) |
| +0x28 | u32 (32b) | count3 |
| +0x30 | ptr → count3 × u32 | u32 array (block read) |
| +0x38 | 14b | INFERRED flags/enum |
| +0x40 | {14b, u64} (`0x852F0`) | INFERRED |
| +0x50 | u32 (32b) | |
| +0x54 | u32 (32b) | |
| +0x58 | u32 (32b) | |
| +0x5C | u32 (32b) | |
| +0x60 | 14b | |

## Per-character record (0xA0 bytes) — `WS+0x7F720`

| off | width / reader | meaning (inferred) |
|---|---|---|
| +0x00 | u64 (`0x6C120`) | **character id** |
| +0x08 | composite (`0x336A40`: 1b + 15b + 7b + …) | INFERRED slot/flags struct |
| +0x10 | 2b | INFERRED |
| +0x14 | 5b | INFERRED (race? class?) |
| +0x18 | 5b | INFERRED (class? sex?) |
| +0x1C | u32 | |
| +0x20 | u32 | |
| +0x24 | u32 | |
| ~+0x28 | array via `0x3374E0`+`0xAB890` (elem = 7b+15b+14b…) | INFERRED equipment/appearance list |
| +0x30 | u32 | |
| ~+0x38 | second array (`0xAB890`) | INFERRED |
| +0x40 | 15b | |
| +0x44 | 15b | |
| +0x48 | 14b | |
| +0x4C | sub-struct (`0xAB810`: 4× `0x6C1C0`) | INFERRED (position/vec) |
| +0x60 | 3b | |
| +0x64 | 1b | |
| +0x68 | 1b | |
| +0x6C | u32 | |
| +0x70 | 4b count → 2× (count × u32) block arrays | |
| +0x88 | u32 count → count × u32 block array | |
| +0x98 | sub-struct (`0x6C1C0`) | INFERRED |

Nested readers still to fully pin (each is more of the same bit-reads; addresses
recorded so it's a finite decode, not a search): `0x336A40`, `0xAB890`, `0xAB810`,
`0x852F0`, `0x6C1C0`, `0x337160`, `0x336C60`. The **character name** is one of the
`0xAB890` arrays (client stores it UTF-16, ≤0x21 chars — see the char-select handler
`WS+0x21540`, which copies name from msg into game state at char+0x08).

## RE method (reproducible, observe-only, no NF)

1. Frida-attach the live client (read-only). Hook `ws2_32!recv` → backtrace located the
   realm receive/parse chain; the message was queued and dispatched on the main thread.
2. Stalker-follow the main thread for one message tick, diff against an idle-tick
   baseline → isolated the 71 message-processing functions → found the pump `0x331990`.
3. The pump does `descriptor = msgMgr->vtable[0x130](opcode)`. Hooked the pump to read
   `msgMgr`'s vtable + the factory ptr, and hooked the factory to capture descriptors.
4. **Sandbox-called** the factory (`0x330910(mgr, opcode)` — a lookup) for every target
   opcode → descriptors → `Read` fn ptrs → static disasm of each `Read` = the wire format.

Nothing malformed was ever sent to the client. All reads observed the client parsing our
own valid messages, or sandbox-called pure lookup functions.

## Next

Build the generic, account-keyed `0x0117` serializer in the engine: read the
authenticated account's characters from `characterdb`, emit this exact bit layout
(LSB-first), send after the account-info messages (0x36/0xAD/0xE7/0x33D — Read fns above,
decode next) that clear "Retrieving Account Information".

## VALIDATED LIVE (2026-08-19) + the remaining blocker

**The 0x0117 body format above is VALIDATED against the real client**: its own `Read`
(WS+0x7FAB0) returns `eax=0` (success) on our serialized message. Two corrections were
needed and confirmed by tracing the client's bit-reader sequence:
- char `+0x4c` is **FIVE** floats (WS+0xAB810), not four.
- top-level has a trailing **`+0x64` 1-bit** field after `+0x60`.

**Transport (realm channel 23115): server->client is a CLEAR frame `[u32 size][u16
opcode][bit-body]`** — NOT the 0x03DC encrypted container (that is a WORLD-channel
construct; the client parses 0x03DC as a normal message here). Proven: sending 0x0117
clear made the client's factory return the 0x117 descriptor (`found=True`) and run Read.
- If S->C encryption is ever needed: **`Encrypt` and `EncryptForClient` produce
  BYTE-IDENTICAL output** (proven in the C++ port's `test_packet_crypt`) — reversing the
  register compensates exactly for the reversed indexing, so the S->C "direction" is a
  no-op and `Decrypt` inverts both. The earlier "the cipher direction was the bug"
  reasoning was WRONG; the real realm-channel fix was CLEAR framing, not the cipher.

**BLOCKER (open): the char-list message parses but is DROPPED.** The char-list HANDLER
(WS+0x21540, which sets `G[0x168]=1` = `CharacterScreenLib.HasReceivedCharacterList()`
and fires the `CharacterList` Lua event via WS+0x21A4C) never runs, because:
- The 0x0117 descriptor has **null handler slots** (fn10/fn18 null); the handler is
  registered at runtime by the char-select STATE object **G** (`*[0x140C66DA8]`), whose
  message handler is vtable slot 11 = WS+0x20EA0.
- **G is NULL** until the client transitions to the char-select pre-game state. G is
  constructed by WS+0x140020730 (0x2d0-byte state obj, 6 callers = pre-game state
  machine). That transition is gated on the realm-enter (0x0592) response / account
  handshake the server must send FIRST.
- The active screen is the client's own `PreGame/Character/Character.lua` (text
  `Pregame_RetrievingCharacters`), which waits for the `CharacterList` event (and
  registers `QueueStatus`/`QueueFinished`/`AccountEntitlementUpdate`/`RealmBroadcast`).

**NEXT: RE (client-only, no NF) the realm-enter -> char-select transition** — what
message(s) after 0x0592 create G / fire the pre-game state change so the 0x117 handler
registers. Then the already-validated char list will display. Candidates to decode from
the client: the queue messages (QueueStatus/QueueFinished) and account-entitlements.
