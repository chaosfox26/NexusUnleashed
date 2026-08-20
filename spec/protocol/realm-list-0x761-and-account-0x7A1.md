# The account-retrieval messages: `0x7A1` (account data) + `0x761` (realm list)

**Status (2026-08-20): both server→client deserializers fully reversed from the client's
own `Read` functions and machine-verified** (tool: `deser.py`, recursive decoder that
re-parses the Read and emits the field tree — it reproduced the hand-derived layout
byte-for-byte). Client-only, NO NF. These are the two pushes the account-retrieval state
(`0x140045A70`, see `account-retrieval-barrier.md`) waits for after realm-enter `0x0592`.

The state machine (measured):

```
connect -> state 1  ("Retrieving Account Information" overlay shown)
  recv 0x7A1 (account data)  -> store to global+0x1638 ; state 1->2   (overlay NOT cleared)
  recv 0x761 (realm list)    -> fire RealmListChanged + NetworkStatus(nil)  => overlay CLEARS,
                                 client advances to RealmSelect            <-- the ADVANCE trigger
  recv 0x36A (4 bytes)       -> tear down conns ; state->5 (handoff/close)
  (two-server only) conn-B op 0x3, needs state 2 -> client SENDS 0x58f, state->3 (dispatcher busy)
```

Dispatcher gate: **state 3 => return busy (ignore all msgs); state 5 => closed.** So messages
process only in state 1/2. `0x761`'s handler has no internal state check, so it can advance
from state 1 directly (0x7A1 first is not strictly required to clear the overlay, but IS
required for the later realm-enter that reuses global+0x1638).

## Bit-stream primitives (all reads are LSB-first, same engine as `0x0117`)

| client fn | meaning |
|---|---|
| `WS+0x6c090(rdr,dst,N)` | read **N bits** into <=32-bit dst (N = the `mov r8d,imm` before the call) |
| `WS+0x6bf60(rdr,dst,N)` | read N bits into 16-bit dst |
| `WS+0x6be30(rdr,dst,N)` | read N bits into 8-bit dst |
| `WS+0x6c120(rdr,dst)`   | read **u64** (fixed 64 bits) |
| `WS+0xa80f0(rdr,dst)`   | read **u32** (fixed 32 bits) |
| `WS+0x337160(rdr,dst,N)`| read **N raw bytes** |
| `WS+0x336a40(rdr,alloc,dst)` | **wide string** = `[1b type][7b len | 15b len][len x u16]` (as validated for 0x0117) |
| `WS+0x3374e0(count*elemsize,alloc)` | dynamic-array alloc; a `for` loop then reads `count` elements |

---

## `0x7A1` ServerAccountData (Read `WS+0xA2110`, factory size 56)

Read order (bit-packed, no padding between fields):

| # | field | width | note |
|---|---|---|---|
| 1 | `f00` | u32 (32b) | |
| 2 | composite @+0x04 (`WS+0xa8900`) | `{ u32, u16, u16, 8 raw bytes }` | the 4 dwords the handler copies to `global+0x1638..+0x1644` — reused when the client later enters a realm (conn-B 0x3 / `0x58f`). 16 bytes. |
| 3 | composite @+0x14 (`WS+0xa8980`) | `{ u32, u16 }` | handler reads +0x14 dword & +0x18 word to build a string |
| 4 | `flag` | 1 bit | |
| 5 | `str` | wstring | |
| 6 | `f28` | u32 (32b) | |
| 7 | `e2c` | 2 bits | |
| 8 | `f30` | 21 bits | |

Minimal valid body: all-zero composites, empty string, zero scalars. The handler stores the
composite-2 dwords for later; zeros are fine to merely advance state 1->2.

---

## `0x761` ServerRealmList (Read `WS+0xAC9D0`, factory size 40) — the ADVANCE trigger

```
u64      header            @+0x00   (WS+0x6c120)   -- 0 is accepted
u32      count1            @+0x08                  -- number of realms
count1 x RealmEntry(0x58)  @+0x10   (WS+0xac130)
u32      count2            @+0x18                  -- second array (broadcast/realm-msg entries)
count2 x Entry2(0x10)      @+0x20   (WS+0xac590)
```

### RealmEntry (0x58 bytes, reader `WS+0xac130`) — Lua fields from RealmSelect.lua

| off | field | width | Lua name / meaning |
|---|---|---|---|
| +0x00 | id | u32 | `nRealmId` |
| +0x08 | name | wstring | `strName` |
| +0x10 | ? | u32 | (candidate `nCount` char-count, or realm group) |
| +0x14 | ? | u32 | |
| +0x18 | pvpType | 2 bits | `nRealmPVPType` (0 = PvE, 1 = PvP; `CodeEnumRealmPVPType`) |
| +0x1c | status | 3 bits | `nRealmStatus` (`CodeEnumRealmStatus`: Up/Standby/Down/Offline/Unknown) |
| +0x20 | population | 3 bits | `nPopulation` (0 Low / 1 Med / 2 High / 3 Full) |
| +0x24 | ? | u32 | |
| +0x28 | ? | 16 raw bytes | |
| +0x38 | address | composite `WS+0xabc00` = `{ 14 bits, u32, wstring host, u64 }` | realm game-server address the client connects to on SelectRealm — **host string + numeric (port?) — NEEDS LIVE VERIFICATION** |
| +0x50 | ? | u16 | |
| +0x52 | ? | u16 | |
| +0x54 | ? | u16 | |
| +0x56 | ? | u16 | |

### Entry2 (0x10 bytes, reader `WS+0xac590`)

```
u32   @+0x00
u8    count3 @+0x04
count3 x 8-byte element  @+0x08   (nested dynamic array)
```
Set `count2 = 0` to omit.

---

## Server implementation plan (C++, all CLEAR framing on the realm channel like 0x0117)

1. **Safest first test — advance with an empty list.** Push `0x7A1` (all-zero) then `0x761`
   with `header=0, count1=0, count2=0` (16-byte body). Expect: overlay clears,
   `RealmListChanged` fires, client shows an (empty) RealmSelect. This proves the mechanism
   with near-zero crash risk. (event-trace `WS+0xEA3E0` should show `RealmListChanged`.)
2. **One realm.** `count1=1`: id=1, name="NexusUnleashed", pvpType=0, status=0(Up),
   population=0, address host = our reachable host, numeric = realm port. Then verify the
   player can select it and reach character retrieval (where the already-validated `0x0117`
   char list is consumed by the char-select state).
3. Open question to settle live: does SelectRealm RECONNECT to the address in the entry, or
   reuse the current socket? If reconnect, the address composite must point back at us.

Tools: `deser.py` (recursive Read decoder — rerun on any new message), the engine's
`inject.txt` message injector (push candidate frames without a rebuild), `event-trace.py`
(hook `WS+0xEA3E0` to watch which Lua events fire). Never brute-force opcodes against the
live client — malformed bodies have crashed it.
