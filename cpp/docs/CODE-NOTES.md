# CODE-NOTES — implementation annotations for `cpp/src`

> This document is the archive of the build notes, reverse-engineering derivations, and wire-layout
> annotations that used to live as comments inside the C++ source. The code is kept **pure** from here
> on; the *why* lives here.
>
> **Order of authority:** `spec/protocol/*.md` remains the protocol spec (the wire formats, opcodes,
> handshake sequences). This file is the implementation-level annotation keyed by source file — the
> offset labels, the "proven live" notes, the provenance, and the parts still marked inferred.
>
> **Provenance discipline:** every file in `cpp/src` is clean-room authored, derived from the 16042
> client (its tables, its deserializers, its cipher) and our own work — **zero lines of NexusForever.**
> Client routine addresses below are `WS+<rva>` into the 16042 `WildStar64.exe` and were read from the
> Hex-Rays decompilation under `Project Resources/_Client-RE/`.

---

## crypto/

### `packet_crypt.h` / `packet_crypt.cpp` — the realm/world packet cipher
Reversed 1:1 from the client and validated offline against a captured plaintext/ciphertext pair (the
`0x0592` container decrypts byte-exact). Both directions use this ONE cipher.

Client routines: key expansion `WS+0xC2EB0`, ctor `WS+0xC2BD0` (also folds the register), encrypt
`WS+0xC2D10` (feedback = output), decrypt `WS+0xC2DE0` (feedback = input). Cross-checked against the
Hex-Rays decompilation (`_Client-RE/.../sub_1400C2D10.c`).

- **key table:** 16 qwords. `key[0] = (kSeedInitial + seed) * MULT`; `key[i] = (key[i-1] + seed) * MULT`.
- **register (initial feedback):** `reg = kSeedInitial`; for each key qword: `reg = (key[i] + reg) * MULT`.
- **process:** qword CFB. `counter = (uint32)(len * (MULT+1))`; `idx = counter & 0xF`, `counter++` per
  block; `out_q = key[idx] ^ in_q ^ reg`; `reg = feedbackOutput ? out_q : in_q`. A byte-wise tail XORs
  the remaining `<8` bytes with `key[idx]` bytes and the register bytes (no feedback update).
- **STATELESS per message** (the register resets to the folded value each message).

Constants: `kSeedInitial = 0x718DA9074F2DEB91`, `kMultiplier = 0xAA7F8EA9`, `kCounterMult = 0xAA7F8EAA`
(`== kMultiplier + 1`).

Keys:
- `AuthChannelKey = WorldChannelSeed = 0xD283F5B34A8DC685`.
- `RealmLaneKey = 0x9A868DE642EF9906` — the realm-lane key. The client re-keys to this fixed constant
  right after it sends `0x058F` (its realm-enter); every C→S message after that (the char-create
  bundle, etc.) is ciphered with it, not `WorldChannelSeed`. RECOVERED live from the client's cipher
  object (Frida: obj key-table `@+0x28` inverts to this seed) and CONFIRMED offline — the create bundle
  decrypts to its sub-message headers `0x025C`/`0x025B`. See `spec/protocol/realm-lane-rekey.md`.

`EncryptForClient` and `Encrypt` are the same routine (feedback = output); `Decrypt` uses feedback =
input.

### `sts_srp.h` / `sts_srp.cpp` — WildStar GAME SRP for the STS login channel
LITTLE-ENDIAN throughout: modulus read LE, `ReverseUInt32` word-order hashing, LE bignums, interleaved
session key, SHA-256 throughout. Mirrors the client parameter-for-parameter so the client's own proof
verifies. Bignum + hash = OpenSSL.

- Modulus `N` = the 128-byte little-endian constant in `NB[]`; generator `g = 2`.
- `k = H(N | g)` with word-order reversal, read LE.
- `I = H(username)`; verifier `v` from authdb is little-endian.
- `StartHandshake`: `b` = random 0x20 bytes → LE bignum; `B = (k*v + g^b) mod N`, returned as 128-byte LE.
- `Verify`: `u = H(A | B)` word-reversed LE; `S = (A * v^u mod N)^b mod N`; session key `K` =
  `interleave_session_key(S_le)`; `M1 = H( H(N)^H(g) | I | salt | A | B | K )`; on match
  `M2 = H(A | M1 | K)`.
- The interleave (WildStar-specific): split `S` from the first zero byte, SHA-256 each half, interleave
  the two digests byte-by-byte into the key.

### `arc4.h` / `arc4.cpp` — the post-SRP STS channel cipher
Standard RC4/ARC4 (public-domain algorithm), written from the algorithm. State `(i,j)` persists across
`ProcessBuffer` calls, matching the C# reference. `arc4.cpp` is a stub TU so the core library has a
stable object for the header-only class.

---

## net/

### `frame.h` — `GamePacketFrame`, the outer wire frame
On the wire a message is `[u32 LE size (self-inclusive)][u16 LE opcode][bit-packed payload]`. PINNED
against the behavioral oracle (`spec/protocol/frame.md`). Header-only. The size field counts the whole
frame including itself.

### `bitstream.h` / `bitstream.cpp` — `PacketWriter` / `PacketReader`
The bit-packed wire format, LSB-first within each byte, matching the client's reader (loads an LE word,
shifts right by the bit position, masks). 1:1 with the C# reference. `WriteWideString(u16string)` writes
each char16 as 16 bits with no length prefix — callers that need the client's length-prefixed string
encode the prefix themselves. `bitstream.cpp` is a stub TU (header-only class) and a home for any future
non-inline helpers.

### `world_packet.h` / `world_packet.cpp` — the encrypted packed container
C++ port of `WorldPacket.cs`. The world channel's encrypted packed container:
- outer frame: `[u32 size][u16 containerOpcode][container payload]`
- container: `[u32 innerLen self-inclusive][encrypted [u16 op][bit-body]]`

`containerOpcode` = `0x03DC` S→C, `0x0244` C→S. NOTE: the realm channel (23115) sends S→C as a CLEAR
frame in the early handshake, not this container — see `spec/protocol/char-list-0x117.md`. This codec is
for the world channel / the inbound `0x0244` decode.

- `ServerContainer = 0x0076`, `ClientContainer = 0x0244`.
- **`ServerContainer` note (VERIFIED LIVE):** the router (`sub_140014F10`) has a NON-NULL decrypt
  handler for `0x76` (`a1[708]`) but a NULL one for `0x3DC` (`a1[709]`) *at account-retrieval time*. So
  the realm/auth S→C container is `0x0076` there (`0x03DC` is world-channel only, decoder not yet
  installed). Decrypt runs `(*(handler[0x10]+0x20))(...)` only if the handler is non-null. **Post
  re-key** (after `0x058F`) the realm lane IS the world channel, and char-select S→C rides `0x03DC` —
  that is the container the world-entry replay proved works there.
- `BuildContainerPayload(serverToClient)` selects the cipher direction: S→C uses `EncryptForClient`, C→S
  uses `Encrypt`. `crypt` is mutated (continuous stream), so it is a non-const reference.

### `game_server.h` / `game_server.cpp` — `GameServer` / `GameSession`
C++ port of `GameServer.cs` / `GameSession.cs`. Asio TCP acceptor that spins a session per connection;
frames are length-prefixed; a client `0x0244` container (world/realm channel) is decrypted and its inner
message dispatched. Handlers are keyed by opcode and are coroutines (they `co_await` sends). Unknown
opcodes are logged, never fatal.

- `SendClearGameMessage` — realm channel S→C uses CLEAR frames in the early handshake.
- `SendGameMessage` — encrypted container when `crypt` is set, else a clear frame.
- `SendGameMessageVia(containerOpcode, …)` — explicit container opcode (`0x76` connection /
  `0x03DC` account/world).
- `crypt` is set on the realm/world channel; `account_id` is correlation set from `AuthSession`.
- `Run()` slices complete frames by the self-inclusive length prefix; a bad container is dropped, never
  fatal. There is a `[RAW IN]` diagnostic trace of outer opcode/size/first-bytes and the decoded inner
  opcode.

---

## sts/

### `sts_message.h` / `sts_message.cpp` — the STS text protocol
C++ port of `StsMessage.cs`. HTTP-shaped: request line `POST /Service/Message STS/1.0`, headers
`l:<bodylen>` / `s:<seq>`, blank line, XML body. Replies: `STS/1.0 200  OK` (**TWO** spaces), `l:`,
`s:<seq>R`. Provenance: measured from the client's `StsConnLib`. `OkStatus` must be that exact form —
the client parses it literally. Request header keys are stored lowercased (case-insensitive lookup).

### `sts_server.h` / `sts_server.cpp` — the async STS login listener
C++ port of `StsServer.cs` (Asio coroutines). One session per connection; requests routed by URI; after
the SRP the channel is ARC4(sessionKey) both ways. Handlers are synchronous and return a reply plus an
optional key that turns encryption on AFTER the reply is sent. `StsSessionState` mirrors
`StsSession.State`. The ARC4 streams are enabled after sending the (plaintext) M2 reply. Handler
exceptions become a 500; unknown URIs a 400.

### `auth_flow.h` / `auth_flow.cpp` — the STS login transaction + realm bridge
C++ port of `AuthFlow.cs` + `AuthSession.cs`. The transaction: Connect → LoginStart → KeyData →
LoginFinish → ListMyAccounts → RequestGameToken over the ported SRP, plus the in-process bridge
(`AuthSession`) from the authenticated account to the realm channel.

- `IAccountStore` is synchronous — the login server is low-concurrency.
- LoginStart returns `KeyDataBlob = [u32 LE len][bytes]` per part (`{ salt, B }`).
- KeyData verifies the client proof; **after M2** (the last plaintext reply) the channel is ARC4(K).
- LoginFinish body uses `AuthType = Password`.
- ListMyAccounts must send the full `GameAccount` field set — a null field makes `WildStar64` crash at
  `strlen(null)` (RVA `0xB3885`). Records are DIRECT children of `<Reply>`; there is **no**
  `<Items>` / `type="array"` wrapper (those strings don't exist in `StsConnLib`).
- `xml_escape` is the `SecurityElement.Escape` equivalent.

---

## proto/

### `character_list.h` / `character_list.cpp` — the `0x0117` char-list body
C++ port of `CharacterListMessage.cs`. Wire layout read from the client's own deserializer
(`WS+0x7FAB0` / `WS+0x7F720`) and VALIDATED live (Read returns `eax=0`). Full map:
`spec/protocol/char-list-0x117.md`. Bits LSB-first.

**Wide-string wire form (`WS+0x336A40`):** `[1b lenType][7b or 15b len][len × u16]`. Name is UTF-8,
widened per code unit (BMP fast path; ASCII is 1:1). A full UTF-8 decode is a later refinement.

**Per-character record (`WS+0x7F720`), 0xA0 bytes in the client struct:**

| offset | field | notes |
|---|---|---|
| +0x00 | `Id` u64 | character id |
| +0x08 | `Name` widestring | `WS+0x336A40` |
| +0x10 | `Sex` | 2 bits |
| +0x14 | `Race` | 5 bits |
| +0x18 | `Class` | 5 bits |
| +0x1c | `WorldId` u32 | `idWorld` (PINNED live) |
| +0x20 | `Level` u32 | `nLevel` — PINNED: a char showed its faction id as its level when `FactionId` sat here |
| +0x24 | countA + visuals | see below |
| +0x30 | countB u32 | appearance list 2 — empty (this is where the OUTFIT/gear will go) |
| +0x40 | 15b, 15b, 14b | |
| +0x4c | vec5 | `WS+0xAB810` reads FIVE floats (LocationX/Y/Z then two zeros) |
| +0x60 | 3 bits | |
| +0x64 | 1 bit | |
| +0x68 | 1 bit | |
| +0x6c | `FactionId` u32 | `idFaction` — read last in the tChar builder; stored last of the three in `WS+0x201F0` |
| +0x70 | countC | 4 bits — empty |
| +0x88 | countD | u32 — empty |
| +0x98 | float | `WS+0x6C1C0` |

**countA (the visuals) — what makes the model render:** reader `WS+0x7F720` loops countA items, each
read by `WS+0xAB890` as `{7b, 15b, 14b, 32b}`; `WS+0x201F0` stores `item[1]` into the model's slot
array at index `item[0]`. So each item is `{slot(7b), displayId(15b), dyeKey(14b)=0, packedDye(32b)=0}`.
Empty countA ⇒ black silhouette (no skin/hair/eye/gear textures). The `Appearance` vector is
`(itemSlot, itemDisplayId)` sourced from `character_appearance`; slot `< 72`, displayId `<= 32767`.

Message envelope (`Build`): `+0x00` header u64 (INFERRED), `+0x08` count, records, then `+0x18` count2,
`+0x28` count3, and the trailing `{14b, 14b+u64, u32×4, 14b, 1b}` block, all zeroed.

### `character_create.h` / `character_create.cpp` — create request `0x025C` + result `0x00DC`
**Request (`CharacterCreateRequest`), wire opcode `0x025C` (dec 604).** After the realm-lane re-key the
body is byte-aligned: `[u32 total][u16 subOp=0x025B][u32 creationId][wideString name][u32 appearance…]`.
The name is a WildStar wide string: `[u8 prefix=(len<<1)|extend][len × u16 LE]`. Confirmed live — a test
name read byte-exact. `CreationId` is the `CharacterCreation.tbl` ID (u32 at body offset 6); it expands
to race/class/sex/faction/start/items via the client's own table (see `game_data`).

**Appearance block** (byte-aligned u32s right after the name): `[count][labelId × count][value × count]`,
each stored as `(real << 3) | tag(0..7)` — shift right 3 to recover the real value. Validated: for
`creationId 511` the labels are exactly the Aurin-female label set and every value falls in its
`CharacterCustomization` range. Parse guards a short/odd body (count sane ≤ 64; name valid; sliders
optional — name + identity still valid without them).

**Result (`CharacterCreateResult`), wire opcode `0x00DC` (dec 220).** The reply to the create request.
Derived from the client's char-select dispatcher (`sub_140020EA0`: opcode 220 → `sub_140021FB0`) and the
result handler `sub_140021FB0`, which reads a u64 character id at struct `+0` and a u32 result at struct
`+12`. Result codes: `3 = OK` (→ world entry), `6 = name-conflict-ish` (error 143523), anything else =
generic failure (error 143525). `Build` layout mirrors the in-memory struct: `charId u64 @0`, a u32 gap
`@8` (unread by the client, kept for alignment), `result u32 @12`. See
`spec/protocol/character-create-0xDC.md`.

### `account_realm.h` / `account_realm.cpp` — account-data `0x07A1`, realm-list `0x0761`, conn-step `0x03db`
The two account-retrieval pushes the client's account state (`WS+0x45A70`) waits for after realm-enter
`0x0592`:
- `0x7A1 ServerAccountData` (Read `WS+0xA2110`) → state 1→2
- `0x761 ServerRealmList` (Read `WS+0xAC9D0`) → fires `RealmListChanged` + clears the "Retrieving
  Account Information" overlay and advances to RealmSelect. An empty list still advances.

Wire layouts reversed from the client's own deserializers and machine-verified (`deser.py`). Full map +
field semantics: `spec/protocol/realm-list-0x761-and-account-0x7A1.md`. Bits LSB-first. Both are sent
CLEAR-framed on the realm channel like `0x0117` in the early handshake.

**`RealmEntry` (client struct `WS+0xac130`, 0x58 bytes):**

| offset | field | notes |
|---|---|---|
| +0x00 | `Id` u32 | `nRealmId` |
| +0x08 | `Name` widestring | `strName` |
| +0x10 | `Field10` u32 | candidate char-count; unconfirmed |
| +0x14 | `Field14` u32 | |
| +0x18 | `PvpType` | 2 bits — `nRealmPVPType` (0 PvE / 1 PvP). We set `2` (RP-PvE data flag, restoration-ready; the stock client renders "PvE" until its UI archive gains the RP branch — see `Claude/Context/RP-PVE-CLIENT-UI-BLOCKER.md`) |
| +0x1c | `Status` | 3 bits — `nRealmStatus`. We set `4` (Up/online); `0` = Unknown showed "?" |
| +0x20 | `Population` | 3 bits — `nPopulation` |
| +0x24 | `Field24` u32 | |
| +0x28 | 16 raw bytes | |
| +0x38 | address composite (`WS+0xabc00`) | `{ 14 bits, u32, wstring host, u64 }` — host + `AddrField10` are the reconnect target (**NEEDS LIVE VERIFY**) |
| +0x50..56 | `Field50..56` u16 | |

**`0x7A1` body:** `u32 · {u32,u16,u16,8B} · {u32,u16} · 1b · wstr · u32 · 2b · 21b`. Zeros advance state
1→2. The composite `@+0x04` (16 bytes) is copied to `global+0x1638` and reused on realm-enter.

**`0x3db` body (Read `WS+0x7D6A0`):** `u32 · u16 · {u32,u16,u16,8B} · u32 · wstring · u32 · 2b · 21b`.
Handled at conn state 9 (`sub_140037F30`): installs the next cipher and advances to state 10.
- **`+0x00`/`+0x04` are the REALM address the client dials next** (`sub_140334BB0`: `htonl(ip)`,
  `htons(port)`). Zeros here made the client `connect()` to `0.0.0.0:0` and hang at "Connecting to
  realm". We write `127.0.0.1` (`0x7F000001`) + port `24000`, and serve the realm connection there.

**`0x761` body:** `u64 header · u32 count1 · count1×RealmEntry · u32 count2 · count2×Entry2` (Entry2
array empty).

### `game_data.h` / `game_data.cpp` — the client's own data tables
Loads the client's own data tables (facts shipped in the 16042 client, uncopyrightable, zero NF) that
the engine is driven from. Exported to TSV via `tbl_reader`.

- **`CharacterCreation`** — one ID expands to race/class/sex/faction/start/starting-items, exactly as
  the character-creation window resolves it. TSV columns: `id, classId, raceId, sex, factionId,
  startEnum, items`.
- **`CharacterCustomization`** — one row: for a `(race, gender)`, the pair(s) `(label, value)` that must
  be chosen for this visual to apply, and the `(slot, displayId)` it produces. `label00 == 0` means a
  base/default row (always applies); `label01 != 0` means a two-condition combo row. TSV columns:
  `raceId, gender, itemSlotId, itemDisplayId, l00, v00, l01, v01`.
- **`ResolveAppearance`** — resolve a character's chosen sliders into the equipped visuals the client
  renders. Rule (validated against a real stored character): a row for `(race, gender)` applies when its
  `label00` (if `!=0`) matches the chosen value AND its `label01` (if `!=0`) matches too; `label00 == 0`
  rows are always-on base visuals. Deduped by slot — last matching row wins in table order, matching the
  client's build.

---

## db/

### `db_store.h` / `db_store.cpp` — the account + character DB stores
C++ port of `DbAccountStore.cs` / `DbCharacterStore.cs`. `authdb.account` (SRP salt/verifier as hex,
gameToken) + `characterdb.character`, via libmariadb (MariaDB :3307). MySqlConnector is MIT; libmariadb
is LGPL (linked, not modified). Connection string is the C# form
`Server=..;Port=..;User=..;Password=..;Database=..`. `DbCharacterStore` swaps the db to `characterdb`
(matches `DbCharacterStore.cs`).

- **`NewCharacter`** — the fields the client sends in a create request (name + race/class/path/sex/
  faction + a server-assigned start location). Appearance/bones carried separately. `Customization` =
  the chosen sliders `(labelId, value)` decoded from the create packet.
- **`CreateCharacter`** — id is generated as `MAX(id)+1` because `character.id` is a manual PK (no
  AUTO_INCREMENT). Persists the raw sliders to `character_customisation (id, label, value)` AND resolves
  them (via `GameData::ResolveAppearance`, the client's own `CharacterCustomization` table) into
  `character_appearance (id, slot, displayId)` so the char-select model renders instead of a black
  silhouette.
- **`GetCharacters`** — attaches each character's stored visuals (`character_appearance`: slot →
  displayId); without them the client renders a black silhouette. Filters `deleteTime IS NULL`.
- **`DeleteCharacter`** — soft-delete (`deleteTime = NOW()`), scoped to the owning account so a client
  can only delete its own. Keeps the row (and its appearance/customisation) recoverable; `GetCharacters`
  excludes deleted rows, so the slot frees immediately.

---

## realm/

### `config.h` — `RealmConfig`, the `realm.json` loader
C++ port of `RealmConfig.cs`. Defaults: realm name `Evindra`, MOTD "Evindra - the original RP-PvE realm
for WildStar (2014).", bind `0.0.0.0`, sts `6600`, auth `23115`, world `24000`. `realm.json` is the
single source of truth for the client-facing realm name.

### `world_handshake.h` / `world_handshake.cpp` — the realm/auth channel and the realm connection
C++ port of `WorldHandshake.cs`. Two servers:

**`Register` (the realm/auth channel, port 23115):** a CLEAR `0x0003` hello on connect, then container
mode. The captured `0x0003` hello body is 47 bytes — byte-for-byte the C# `HelloBodyHex`; the
`0b14332f01` stamp sits at byte 26 and the client validates message definitions from it (a shifted stamp
is "Message Definitions Mismatch").

On `0x0592` realm-enter (token-bearing):
1. **Realm-hello response `0x0591`** (u32, bit0 = flag). The client's connection dispatcher
   (`WS+0x370D0`) advances conn state 6→9 on receiving this (guard: state must be 6 or 8, exactly where
   it parks). Without it the connection never completes and the account state never arms. Sent via the
   `0x76` container.
2. **`0x03db` conn handshake step 2** — installs the SECOND (`0x3dc`) cipher, state 9→10.
3. Connection *completion* (op-3 on the realm lane) does NOT happen over this socket — the realm lane is
   its OWN connection. After `0x03db` the client dials the realm address we put in the `0x3db` body
   (`127.0.0.1:world_port`) and the char-select handshake happens there.

Account-retrieval handshake (configurable via `SendAccountData` / `SendRealmList` / `IncludeRealm` so
the empty-list vs one-realm test can be flipped without touching the handler):
- `0x7A1` account data → account state 1→2
- `0x761` realm list → fires `RealmListChanged` + `NetworkStatus(nil)` ⇒ overlay clears, client advances
  to RealmSelect. Empty list still advances.
- Realm-channel S→C is CLEAR framing (PROVEN LIVE): the client's inner-msg Read runs on a clear `0x761`
  (`eax=0`) but NOT on a `0x03DC` container here (the client can't decode our S→C container before the
  decoder is installed). So encryption/container is a red herring at this step; the block is that the
  connection handshake never completes, so the account state never arms to dispatch. Account/realm/char
  data go via the SECOND (`0x03DC`) container, whose decrypt cipher the client installs when it processes
  `0x3db` (state 9→10).
- `SendAccountData` / `SendRealmList` default false in the header's static init because sending them at
  the "Connecting to realm" stage breaks the realm connection; they are held until the right step.

**`RegisterRealmConnection` (the realm connection, port `world_port` = 24000):** the client dials this
after `0x3db`. The connection object is already at state 10; this socket is bound to its realm lane
(channel index 1). A CLEAR `0x0003` takes the client's hello path, which validates against a fixed
channel id and is dropped on the realm lane (proven: it never reached the dispatcher). An ENCRYPTED
`0x76` container routes through the multi-lane dispatch, matches the realm lane, and op-3 hits
`sub_140038120` — which creates the account object and completes the connection (state 10→11). So on
connect we send an encrypted `0x0003` hello, then serve the account's `0x0117` character list (empty
list → the "Create Character" button / char creator).

Handlers on the realm connection:
- **`0x058F`** = the client's realm-enter (token-bearing). It is the LAST message ciphered with the auth
  key: right after sending it the client re-keys to `RealmLaneKey`, so we re-key `s.crypt` here too.
  (This message itself was already decoded with the auth key before the handler ran.)
- **`0x07A4`** = realm-list REQUEST (the "Change Your Realm" screen). Without a reply it hangs on
  "Retrieving realm list". Answer with `0x0761` carrying our realm entry (Evindra), post-re-key via
  `0x03DC`.
- **`0x07DF`** = the client entered the selected realm from that screen (body = u32 realm id). Without a
  reply it hangs on "Retrieving Characters". Serve the account's `0x0117` char list via `0x03DC`.
- **`0x025C`** = `ClientCharacterCreate` (Enter Game on the creator's Finalize page). Body readable
  after the re-key. Parse the name, persist, refresh the list, send the `0xDC` result — all S→C via the
  `0x03DC` world-channel container (this is why the create result previously only landed "on
  reconnect"). On failure, send `0xDC` GenericFail rather than hang.
- **`0x0352`** = `ClientCharacterDelete` (the Delete button). Client sends message 850
  (`sub_140024C10`) with body = u64 characterId. Soft-delete, then send the `0xE6` result via `0x03DC`;
  the client's dispatcher (`sub_140020EA0`, opcode 230) removes the character when `result == 0`, which
  frees the slot. Body is two byte-aligned u32s `{result, 0}`.
- **`0x07DD`** = Enter Game on a selected character (body = u64 characterId) — the world-entry trigger.
  Currently a clean stub (logs the charId). The NF-lineage replay approach (streaming
  `captures/world-entry-replay.bin`) was REMOVED (commit `cdc62fc`); world entry is now built by hand,
  message by message, from the client's own deserializers + our DB (`spec/protocol/world-entry.md`).

**Host wiring:** `CharacterListBodyProvider` (accountId → `0x0117` body), `CreateCharacterProvider`
(accountId, decrypted body → new char id, 0 on failure), and `DeleteCharacterProvider` (accountId,
characterId → true if deleted). The providers keep the networking layer free of a DB dependency. (The
former `WorldEntrySequence` replay provider was removed with the NF replay — see `0x07DD` above.)

**Diagnostics (dev-only helpers in `world_handshake.cpp`):** `LoadInject()` reads
`inject.txt` lines `<opcodeHex> <bodyHex>` and sends them as CLEAR frames on realm-enter to probe the
account-retrieval handshake without rebuilding (absent file = no-op). `HexDump()` is a canonical
hex+ascii dump for pinning unknown wire payloads offline.

### `main.cpp` — the realm host entry point
C++ port of `Program.cs`. Boots the STS login server + the realm/auth channel + the realm connection
server; the world channel arrives later. `stdout` is unbuffered so log lines flush immediately.
`realm.json` is the single source of truth for the client-facing realm name.

- Loads the client data tables (`character-creation.tsv`, `character-customization.tsv`) at boot.
- `CreateCharacterProvider`: parse the create body; name falls back to `NexusHero` if unparsed; resolve
  race/class/sex/faction from the client's own `CharacterCreation` table (the packet only carries the
  creation ID). `ActivePath`, `WorldId`, `WorldZoneId` are TODO (path is a separate field still to pin;
  world comes from `startEnum` → starting zone). Sliders → stored + resolved to visuals.
- The former `captures/world-entry-replay.bin` loader (the NF-lineage `0x07DD` replay) was REMOVED
  (commit `cdc62fc`). World entry is built by hand from the client's deserializers — no replay loaded.
- **Worker pool:** one worker per hardware thread by default. `nusl.exe` overrides the count via
  `NUSL_THREADS` to match its CPU-cores slider; the process affinity mask it sets pins the pool to those
  cores. Prints `worker pool: N threads`.

---

## launcher/

### `nusl.cpp` — nusl.exe, the Nexus Unleashed Server Launcher
A polished NATIVE control panel + resource governor for `nexus_realm.exe`. Rendered with GDI+
(gradients, glows, rounded panels, custom sliders) — no .NET, no WebView2, no runtime deps, a tiny
self-contained exe that ships next to `nexus_realm.exe` + `realm.json`.

- start / stop, live status, log tail (reads `nexus_realm.log`).
- **MEMORY CAP** slider (1..N GB) enforced with a Windows Job Object (`JOB_OBJECT_LIMIT_JOB_MEMORY` +
  `KILL_ON_JOB_CLOSE`).
- **CPU CORES** slider — process affinity mask + worker-pool size via `NUSL_THREADS`.
- live RAM (working set, `GetProcessMemoryInfo`) + CPU% (`GetProcessTimes`) readouts, active by default
  (500 ms poll).

Implementation notes:
- `<objidl.h>` must be included before `<gdiplus.h>` (IStream etc. — GDI+ headers need it, trimmed by
  `WIN32_LEAN_AND_MEAN`).
- Palette (from the roadmap): black canvas, magenta + blue accents, white text.
- Dark mode: dark title bar via `DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE=20)`; dark (grey)
  scrollbar via `SetPreferredAppMode(AllowDark)` (uxtheme ordinal 135) + `SetWindowTheme(log,
  "DarkMode_Explorer")`.
- App icon = the operator's Nexus Unleashed emblem (`nusl.ico`, resource id 1), set on the window class,
  `WM_SETICON` small + big, and taskbar/alt-tab.
- A read-only EDIT reports as static, so both `WM_CTLCOLORSTATIC` and `WM_CTLCOLOREDIT` paint the dark
  log colors.
- `Text()` y is the TOP of the text line (StringAlignmentNear), so labels don't sit low.
- The `HitSlider` lambda was named `inTrack` because `near` is a Windows macro.
- The launcher is the **measurement tool** for the optimization mission (`OPTIMIZATION.md`): dial in
  "all worlds under N GB across M cores" and watch it live.
