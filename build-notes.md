# NexusUnleashed — BUILD NOTES (the go-to record)

_Last updated 2026-08-19. **This is the single source of truth for what has been built.**
Claude's continuation docs (`Claude/Context/CONTINUE.md`, `STATE.md`, `CPP-PORT-PLAN.md`)
point here. Read this first to see where the project stands._

---

## 0a. LATEST (2026-08-20) — the barrier is reversed, implemented, and staged

The account-retrieval barrier (§5) is **solved on paper and built into the C++ engine**,
awaiting one live test. What changed this session:
- Mapped the client's account state machine (dispatcher `WS+0x45A70`): it waits for `0x7A1`
  (account data -> state 1->2) then `0x761` (realm list -> fires `RealmListChanged` + clears
  the "Retrieving Account Information" overlay = the advance to RealmSelect).
- **Reversed both server->client deserializers** from the client's own `Read` functions and
  **machine-verified** them with a new tool, `deser.py` (recursive Read-function decoder — it
  reproduced the hand-derived field tree). Wire map: `spec/protocol/realm-list-0x761-and-account-0x7A1.md`.
- **Implemented C++ serializers** (`cpp/src/proto/account_realm.*`) + **wired the push** on
  realm-enter (`realm/world_handshake.cpp`, before the char list; toggles for empty-list vs
  one-realm). Built clean, unit tests green (0x7A1=34 B, empty 0x761=16 B).
- Privacy: redacted a character name from a session doc and a test fixture; deleted 21 stale
  credential/character screenshots + 6 packet captures. `privacy-guard` CLEAN.
- **Next = one LIVE test** (client relaunch + login): does the overlay clear and does the
  client reach RealmSelect / character retrieval. Then the world.

## 0. TL;DR — where we are

**The engine is now C++.** A real WildStar **16042** client authenticates **end-to-end
against the C++ engine**, enters the realm channel, and is served its character list
from the database — all proven live. The C++ port has reached **parity with the old C#
prototype** and surpassed it as the project's primary engine.

**C# is now an afterthought** — a historical reference/oracle that got us the protocol
and crypto, kept only until it is no longer useful. All new work is C++.

The one remaining step to "standing in the world" is a single protocol-RE problem (the
realm-enter → character-select transition, §5) that is **language-neutral** — it was
always the next thing, for either engine.

---

## 1. What NexusUnleashed is (the vision)

A clean-room, from-scratch, **MIT** WildStar server + engine that owes AGPL / NexusForever
**nothing** and is free for the whole community. But it is bigger than a server: the goal
is heavy game-engine work and **new rendering features that never existed — FSR 3/4,
DLSS 3/4, and a DX12 renderer** — plus a community content platform. Those are unavoidably
C++, which is why the whole project unified on C++. Full scope + rationale:
`Claude/Context/CPP-PORT-PLAN.md`.

**Hard rules (never broken):** NO NexusForever — not servers, source, protocol, or
captures. Every protocol fact comes from **the client itself** (its binary, tables, Lua)
and **our own DB/data**. See `Claude/Context/CONTINUE.md` §2 for the full rulebook.

---

## 2. The C++ engine — what's built

Location: **`cpp/`** (inside this repo, beside the specs and the C# reference).
Toolchain: **Visual Studio 18 / MSVC 19.51, C++20, CMake + Ninja**. Dependencies via
**vcpkg manifest mode** (`cpp/vcpkg.json`): OpenSSL, Asio, libmariadb, nlohmann-json.

| Component | Files | Status |
|---|---|---|
| Bit-packed reader/writer (LSB-first) | `net/bitstream.h` | DONE, round-trip verified |
| Packet cipher (Carbine's) | `crypto/packet_crypt.*` | DONE, verified |
| Frame codec (`[size][op][body]`) | `net/frame.h` | DONE |
| Encrypted container codec | `net/world_packet.*` | DONE, round-trip verified |
| Char-list `0x0117` serializer | `proto/character_list.*` | DONE, byte-exact |
| SRP login (game-SRP, little-endian) | `crypto/sts_srp.*` | DONE (OpenSSL bignum+SHA), **verified vs live client** |
| RC4 / ARC4 stream cipher | `crypto/arc4.h` | DONE, canonical KAT |
| STS message model (req/reply/parser) | `sts/sts_message.*` | DONE, verified |
| STS async server + session | `sts/sts_server.*` | DONE (Asio coroutines) |
| Login transaction (AuthFlow) | `sts/auth_flow.*` | DONE, **client authenticates** |
| Realm game server + session | `net/game_server.*` | DONE (Asio) |
| Realm handshake (hello + char list) | `realm/world_handshake.*` | DONE, **realm-enter served** |
| DB stores (account + character) | `db/db_store.*` | DONE (libmariadb, MariaDB :3307) |
| Config + host | `realm/config.h`, `realm/main.cpp` | DONE |
| Unit tests (byte-verification) | `tests/*` | ALL GREEN |

Everything above is **proven against the real 16042 client**, not just unit tests.

---

## 3. How to build & run

```
# configure (vcpkg fetches/pins deps; first run builds OpenSSL etc.)
cmake -S cpp -B cpp/build -DCMAKE_TOOLCHAIN_FILE=<home>/vcpkg/scripts/buildsystems/vcpkg.cmake
# build
cmake --build cpp/build --config Release
# unit tests
cpp/build/Release/nexus_tests.exe
# run the realm (needs cpp/build/Release/realm.json — gitignored, holds the DB conn)
cpp/build/Release/nexus_realm.exe
```
Ports: STS **6600**, realm/auth **23115**, world **24000**. DB: bundled MariaDB **:3307**
(authdb + characterdb). `realm.json` mirrors the C# connection string.

**Test loop (same as we've used all along, language-independent):** run `nexus_realm.exe`,
drive the client login (see `Claude/Context/local-notes.md`, gitignored), and verify with
the Frida scripts in `Project Resources/Tools-Working/Tools/re/` + scratchpad. The client
is the ultimate byte-verifier.

---

## 4. Live proof (2026-08-19) — the C++ server log

```
[STS-SRP] proof VERIFIED (game-SRP little-endian)          <- SRP verified in C++
realm: <- 0x0592 realm-enter (396B)
realm: character-list provider: account 2 has 1 character(s) <- DB read via libmariadb
realm: -> 0x0117 character list (clear frame) for account 2 (121B) <- validated serializer
realm: <- inner op=0x0000 (1B)
```

**Two bugs the live client / byte-verification caught (and we fixed):**
1. `Encrypt` and `EncryptForClient` are byte-identical (register reversal cancels index
   reversal) — the S->C cipher "direction" is a no-op; the real realm fix was CLEAR framing.
2. The realm hello had its `0b14332f01` **message-definitions stamp** hand-typed 2 bytes
   early → client error *"Message Definitions Mismatch — Connection closed by remote host"*.
   Fixed to match the C# bytes exactly.

---

## 5. The ONE remaining step to the world (language-neutral) — SHARPENED

**A window-only screenshot corrected the earlier guess:** the client is still on the
**Login screen** (`PreGame/Login/Login.lua`) showing the network-status overlay
**"Network Status: Retrieving Account Information."** It is NOT on RealmSelect or the
Character screen yet — so the char-select object **G** (`*[0x140C66DA8]`) being null is
EXPECTED (we haven't reached char-select). The client is blocked one stage earlier: after
realm-enter (0x0592) it is **retrieving account info** and won't advance to RealmSelect /
Character until the server sends that account data.

So the char list we serve is (correctly) ignored for now — the client wants the
**account-retrieval messages first.** The transition is C++-driven (fires the Lua
`NetworkStatus` / `RealmListChanged` events). Relevant client code in the account/realm
handoff region `0x14004xxxx`: `WS+0x140046094` fires `RealmListChanged`, `WS+0x14003E5B0`
references `CodeEnumRealmStatus`, and the char-select state G is constructed by
`WS+0x140020730` (6 vtable-dispatched pre-game-state callers, e.g. `WS+0x140046340`).

**NEXT (client-only, no NF): find the account-retrieval message(s) the client expects
after 0x0592** (entitlements / realm-list / a realm-enter response) that clear
"Retrieving Account Information" and advance the pre-game state machine. Then RealmSelect →
character retrieval → the already-validated char list displays → **world.**

**Experimentation tools built for exactly this (2026-08-19):**
- **Message injector** — `cpp/src/realm/world_handshake.cpp` reads
  `cpp/build/Release/inject.txt` (lines `<opcodeHex> <bodyHex>`) and sends those CLEAR
  frames on realm-enter *before* the char list. Edit the file + reconnect to probe
  candidate messages **without rebuilding**.
- **Window-only screenshot** — `<scratch>/ws-shot.ps1` captures ONLY the WildStar
  window (never the whole screen — operator privacy rule). Use screenshots as references
  when stuck (operator's method).
- Frida RE scripts (client-side, read-only): factory descriptors, pump, dispatcher, the
  char-select object G reader (`read-g.py`), bit-read sequence tracer, etc.

**Full RE state + tools + next approaches: `spec/protocol/account-retrieval-barrier.md`.**
Also `spec/protocol/char-list-0x117.md` + `Claude/Context/CONTINUE.md`.

---

## 6. What carries over (the durable value)

- **`spec/protocol/*.md`** — the protocol/crypto/wire-format specs (language-neutral, the
  port's source of truth).
- **RE tooling** — Frida + disassembly + Python (`Project Resources/Tools-Working/Tools/re/`),
  hooks the CLIENT, works regardless of server language.
- **DB schema**, all knowledge docs, ledgers, and the provenance discipline.

## 7. The C# tree — an afterthought now

`src/NexusUnleashed.*` (C#) got us here: it cracked the STS/SRP login, the cipher, the
framing, and the `0x0117` wire format, and served as the byte-for-byte oracle during the
port. **It is no longer the primary engine.** Keep it only as long as it's a useful
reference; the C++ engine in `cpp/` is the project. Do not add features to the C# tree.

## 8. The road ahead (sequenced)

1. Solve §5 (realm-enter → char-select) in C++ → **character select → world**.
2. World channel (24000) + world entry.
3. Then the big vision: FSR 3/4, DLSS 3/4, DX12 renderer, engine work, community content.
   FSR1 is an early win; FSR2/3/4 + DLSS need motion vectors the engine doesn't expose
   (deep renderer RE); DX12 is a renderer replacement. Multi-year, sequenced.
