# THE C++ PORT — decision, full vision, and phased plan (2026-08-19)

_Read this alongside `CONTINUE.md` (mission + rules) and `STATE.md` (resume banner).
This is the master plan for porting the clean engine from C# to C++ and the long-term
vision it serves. **Operator decision, 2026-08-19: "If you can 1:1 what we already
have, go for it. Let's do this."**_

---

## 0. The decision

**Port the entire clean engine to C++, 1:1 with the current C# implementation, now.**
The C# tree becomes the **reference oracle** — every C++ component is byte-verified
against it before we move on. The C# is NOT thrown away until C++ overtakes it.

Language was debated in full (2026-08-19). The honest ranking for a *pure server* was
Rust ≈ C# > C++ on the "fast + stable + handles strain" axes (memory safety + no GC
tail-latency). **But the FULL scope flips it to C++** — see §1. The operator is an
optimization-first builder and wants one coherent native project.

---

## 1. THE FULL VISION (why C++ — this is bigger than a server)

Nexus Unleashed is a **complete WildStar revival**, not just a realm server. The
operator's stated scope (2026-08-19):

1. **Merge the friend's work** into the project (the friend does in-game/engine work
   — e.g. taxi/transport — in **C++**, does not know Rust).
2. **Port to Linux** eventually ("Lennox").
3. **Heavy work on the game ENGINE itself** — reverse-engineer + extend the client's
   renderer and gameplay engine.
4. **New rendering features that never existed:** **FSR 3/4, DLSS 3/4, and a DX12
   renderer.** (WildStar shipped on older Direct3D.) This is the north-star ambition.
5. **Optimize for stability + bug-fixing.**
6. **Optimize for community content** — new quests, implementations, a modding
   platform.

**The pivotal fact that makes it C++:** items 3–4 (engine/renderer/DLSS/FSR/DX12) are
**unavoidably C++** — the DLSS SDK, FSR, Direct3D hooking, and a compiled proprietary
game engine all live in C++, and the friend is already there. When half a project is
locked to a language, that half is the center of gravity. Unifying the server on C++
too (rather than a Rust/C# server + C++ client) keeps one toolchain, one skillset, and
lets work flow between server and engine. The stability C# gives for free is bought
back in C++ with discipline (fuzz the packet parser, sanitizers in CI, per-connection
crash isolation).

**Honest difficulty ladder for the rendering goal (set expectations, don't kill the
dream):** FSR1 (spatial post-process) is a weekend-class injection win. **FSR2/3/4 and
DLSS3/4 need per-frame motion vectors + depth the WildStar engine does not expose** →
deep renderer RE to reconstruct them. **DX12** is a renderer replacement. This is a
multi-year summit; sequence it, don't boil the ocean. The RE skillset we're using on
`WildStar64.exe` right now IS the client-engine skillset — it's continuous.

**Licensing:** the clean-room / no-NF discipline is unchanged by language (see
`CONTINUE.md` §2). Client rendering work (DLSS SDK = NVIDIA license, FSR = MIT) is a
*different* licensing surface from the server's AGPL-avoidance concern — keep them
mentally separate.

---

## 2. WHAT CARRIES OVER UNCHANGED (the port is mostly re-implementing a thin layer)

The **value is in the specs, not the C# code.** These are language-neutral and are the
port's source of truth:

- **`spec/protocol/*.md`** — STS/SRP, cipher (`PacketCrypt` two-phase keying),
  containers/framing, **`char-list-0x117.md`** (the validated char-list wire format +
  every bit-reader address), observed opcodes, world-entry order.
- **The RE tooling is language-agnostic.** Frida + disassembly + the Python scripts in
  `Project Resources/Tools-Working/Tools/re/` and the scratchpad hook the CLIENT, not
  the server. Protocol discovery continues identically no matter what the server is
  written in — the server just has to emit the right bytes, verified with the client.
- **Our DB schema** (authdb/characterdb on MariaDB :3307) is unchanged.
- **All knowledge docs, ledgers, and the provenance discipline** carry over freely.

So the C++ effort is: re-implement a small, fully-specified networking/crypto/DB layer,
byte-checking each piece against the C# reference.

---

## 3. CURRENT PROTOCOL STATE (what the C++ must reproduce)

From this session (all clean, client-only, NO NF — see `CONTINUE.md`, `STATE.md`,
`spec/protocol/`):

- **STS login: DONE end-to-end.** Real 16042 client authenticates (game-SRP
  little-endian; ARC4 post-SRP channel), `ListMyAccounts` (records direct under
  `<Reply>`), `RequestGameToken`, realm handoff.
- **Realm channel (23115): server→client is a CLEAR frame** `[u32 size][u16 op][body]`
  (NOT the 0x03DC encrypted container — that's world-channel only). Inbound client
  containers are `0x0244` (auth-key `PacketCrypt`). Realm-enter inner op = **`0x0592`**.
- **Char list `0x0117`: wire format VALIDATED** — the client's own `Read` (WS+0x7FAB0)
  returns success on our serialized message. Full field map in
  `spec/protocol/char-list-0x117.md` (5 floats at char+0x4c, top +0x64 1-bit, etc.).
- **OPEN BLOCKER:** the char list parses but is **dropped** — the char-select C++
  state object **G** (`*[0x140C66DA8]`) is null until the realm-enter→char-select
  handshake transitions the client's pre-game state machine (G ctor WS+0x140020730).
  The client's own `PreGame/Character/Character.lua` shows `Pregame_RetrievingCharacters`
  and waits for the `CharacterList` event (fired by WS+0x21A4C inside the 0x117 handler
  WS+0x21540, which needs G). **NEXT RE (client-only): the message(s) after 0x0592 that
  create G / register the 0x117 handler.** Then the validated char list displays →
  character select → world entry.
- If S→C encryption is ever needed: `PacketCrypt.EncryptForClient` (inverse of
  `Decrypt`), NOT the forward `Encrypt`.

---

## 4. THE C# INVENTORY TO PORT (component map + C++ dependency choices)

Current projects under `src/`:

| C# project / file | C++ target | Dependency |
|---|---|---|
| `Network/PacketReader.cs`, `PacketWriter.cs` | `net/bitstream.{h,cpp}` | none (hand-rolled, LSB-first) |
| `Cryptography/PacketCrypt.cs` | `crypto/packet_crypt.{h,cpp}` | none (pure math; use `wrapping`-style explicit overflow) |
| `Cryptography/SRP6a.cs`, `StsSrp.cs`, `SrpServer.cs`, `ARC4.cs`, `Adler32.cs`, `Rng.cs` | `crypto/srp.{h,cpp}`, `crypto/arc4.{h,cpp}` | **OpenSSL** (BIGNUM + SHA) or GMP + a SHA lib |
| `Network/GamePacketFrame.cs`, `WorldPacket.cs`, `GameSession.cs`, `GameServer.cs` | `net/frame.*`, `net/world_packet.*`, `net/session.*`, `net/server.*` | **Asio** (standalone) + C++20 coroutines |
| `Network/CharacterListMessage.cs`, `Opcodes.cs`, `GameMessageOpcode.cs`, `ServerMessages.cs`, `WorldMessageRouter.cs` | `proto/messages.*`, `proto/opcodes.h` | none |
| `Sts/AuthFlow.cs`, `StsServer.cs`, `StsReply.cs`, `XmlBody.cs`, `AuthSession.cs` | `sts/*` | Asio (TCP) |
| `Database/DbAccountStore.cs`, `DbCharacterStore.cs` (MySqlConnector) | `db/*` | **MySQL Connector/C++** or libmariadb (or SOCI) |
| `Realm/Program.cs`, `WorldHandshake.cs`, `RealmConfig.cs` | `realm/main.cpp`, `realm/world_handshake.*`, config | **nlohmann/json** (realm.json), **spdlog** (logging) |
| `GameData*`, `Content` | port on demand | only what login→world needs at first |

**Toolchain:** CMake + vcpkg (dependency manager). Compiler: MSVC (Windows now) →
clang/gcc for Linux later. C++20 (coroutines for async).

**Dependency shortlist to lock in Phase 0:** asio (standalone, header-only),
openssl (bignum+hash+arc4), mysql-connector-cpp (or libmariadb), nlohmann-json,
spdlog, and a test framework (Catch2 or doctest) for the byte-verification vectors.

---

## 5. THE PHASED PLAN (each phase is an independently testable milestone)

- **Phase 0 — skeleton. [DONE 2026-08-19]** CMake + vcpkg project (`NexusUnleashed-Engine/cpp/` or a new
  repo — decide), deps locked, a `hello`-level build green on Windows. Wire spdlog +
  a Catch2 test target.
- **Phase 1 — pure leaves. [DONE 2026-08-19 — bitstream, packet_crypt, frame, world_packet, character_list; all byte-verified]** Port bitstream (reader/writer), PacketCrypt (all three
  ops: Encrypt / Decrypt / EncryptForClient), framing. **Byte-verify** against C#
  test vectors (generate vectors from the C# side once, assert in C++ tests). This is
  fast and de-risks the foundation.
- **Phase 2 — SRP + STS login. [DONE 2026-08-19 — client authenticates against C++]**
- **Phase 3 — realm channel + char list. [DONE 2026-08-19 — realm-enter + DB-backed 0x0117 served; client at parity with C#]** Port SRP (game-SRP little-endian — the fiddly part,
  but fully spec'd) + the STS server + AuthFlow. **Milestone: the real client
  authenticates against the C++ server end-to-end** (drive login, watch STS log).
- **Phase 3 — realm channel + char list.** Port GameSession/GameServer (Asio),
  WorldHandshake, the container codec, and the CharacterList serializer. **Milestone:
  the client's factory returns the 0x117 descriptor and its Read succeeds** — verified
  with the SAME Frida tracers used this session (`read-watch.py`, `diag.py`).
- **Phase 4 — finish the handshake→world RE, natively in C++.** Solve the open blocker
  (§3), character select, world entry. From here the C# reference can be retired.

**Discipline (non-negotiable):** no phase is "done" until its C++ output is proven
byte-identical to the C# reference (or, for the frontier RE, proven with the client via
Frida). Keep the C# tree building until Phase 4 passes.

---

## 6. HOW TO TEST (same loop as this session, language-independent)

The engine runs as a built exe; the real client is driven into it and observed with
Frida — none of this cares about C# vs C++:
- Run the (C++) engine exe; it listens STS 6600 / realm 23115 / world 24000.
- Drive the client login (see `local-notes.md` for the driver + test-account handling).
- Verify with the Frida scripts (client-side, read-only): factory descriptors, the
  message `Read` return code, the bit-read sequence, the char-select object G.
- MariaDB is the bundled instance on **:3307** (authdb + characterdb).

Hardware + standing rules (hardware-first, no-NF, never drive the client's mouse beyond
the login click, leave the stack up) are in `CONTINUE.md`. Sensitive specifics
(test account, character, machine, driver paths) are in the gitignored `local-notes.md`.

---

## 7. OPEN DECISIONS (ask the operator before assuming)

- **Repo layout:** new C++ inside a `cpp/` subdir of the current MIT repo, or a fresh
  sibling repo? (Leaning: a `cpp/` tree in the same repo so specs + C# reference sit
  beside it, then split later if desired.)
- **DB client lib:** MySQL Connector/C++ vs libmariadb vs SOCI (leaning Connector/C++
  for async-ish + maintained; confirm license = clean).
- **Async style:** Asio C++20 coroutines (leaning yes — closest to the C# async we're
  porting) vs callback/thread-per-connection.
