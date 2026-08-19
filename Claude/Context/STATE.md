# NexusUnleashed Engine — State of the Build

> **RESUME HERE (2026-08-19, deep night) — STS DONE END TO END; THE CLIENT IS NOW ON
> THE REALM CHANNEL (23115). NEXT WALL = CHARACTER LIST.**
> The real 16042 client authenticates fully through STS and hands off to our realm
> server. Milestones this session, all clean (client-as-oracle, NO NF server — the
> operator's hard line: "We do NOT use NF servers"):
> - **STS SRP cracked** (game SRP, little-endian — see below) AND the full STS
>   transaction chain now works: `/Sts/Connect` → `LoginStart`/`KeyData` (SRP proof
>   VERIFIES) → `LoginFinish` (**AuthType=`Password`**, not "1") → `ListMyAccounts`
>   → `RequestGameToken` (`<Token>`). Post-SRP channel is **ARC4(sessionKey)**.
> - **ListMyAccounts fix:** records are direct children of `<Reply>` — **NO
>   `<Items>`/type="array" wrapper** (those strings do not exist in StsConnLib; the
>   parser does `[reply+0x60]`=first record, `[item+0xc0]`=field). Enriched the
>   GameAccount with the FULL field set the client reads (GameAccountId, AccountId,
>   LoginName, UserId, UserName, Email, Alias, AccountAlias, GameCode, AppId,
>   UserCenter, State, Status, Roles) — a missing string field made WildStar64.exe
>   `strlen(null)` → AccessViolation (RVA 0xB3885). Fixed → token issued.
> - **Realm/auth channel (23115):** the client accepts a CLEAR `0x0003` hello, then
>   speaks the auth-key encrypted container (`0x0244` in / `0x03DC` out). Wired
>   `WorldHandshake` to bootstrap clear-then-container (keys off `Crypt==null`).
>   The client's `0x0244` **decodes cleanly to inner op `0x0592`** (396B) = realm-
>   enter: `[build 16042][8B][login name UTF-16][fields][hardware survey]`. Our
>   earlier capture called this `0x058F`; **the LIVE client uses `0x0592` — it wins.**
> - **I drive the login myself** (screenshot + PowerShell SendInput to WildStar64;
>   `<scratch>/wslogin.ps1`). Engine log: `<scratch>/clean-engine.log`.
> - **PRIVACY:** the 0x0592 body carries the login email + machine hardware — NEVER
>   commit a capture of it. Code changes hardcode nothing private (login comes from
>   the client at runtime).
> - **NEXT: reply to `0x0592` → CHARACTER LIST → select → world entry.** No model
>   exists yet; needs RE (opcode + field layout). `HasReceivedCharacterList` (Lua)
>   confirms the message. Our world-entry capture SKIPPED char-select (it was a
>   reconnect), so `world-entry.md`'s 0x0988/0x0981/0x0117/0x0262 sequence is
>   POST-select. The realm must also read the account's real characters from
>   characterdb — the engine does not read that DB yet.
>
> ---------- (prior) THE LOGIN IS ALMOST CRACKED. ----------
> The STS login broke through "NC Platform Error 15": the real 16042 client now
> **accepts our LoginStart reply and sends its SRP proof** (`/Auth/KeyData`).
> What got us there, all confirmed against the client + a wire capture:
> - Reply envelope is **`<Reply>`** (not `<Content>`); status line is
>   `STS/1.0 200  OK` (TWO spaces — single-space is silently discarded → error 15);
>   `s:<seq>R` framing; `<KeyData>` = base64 of `[u32 saltLen][salt][u32 BLen][B]`.
> - SRP is **standard OpenSSL SRP-6a, big-endian**; M1 is 32 bytes → **SHA-256**.
> - **ROOT-CAUSE BUG FOUND + FIXED:** the DB verifier is stored **little-endian**
>   (.NET `BigInteger.ToByteArray()`, 129 bytes incl. sign byte) — we were reading
>   it big-endian, which poisoned `server_S` for every k/recipe (why no proof ever
>   verified). `StsSrp` now reads it little-endian; self-test 7/7.
> - The server now **auto-searches** the SRP recipe (k-rotation ×7, u × K × salt ×
>   M1) against the client's own M1 and accepts the match — so the NEXT real login
>   should self-resolve the recipe. Old captured proofs are POISONED (made with the
>   big-endian-v B) — need a FRESH login to confirm.
> **The remaining step is a single confirmation login.** Operator wants it done
> autonomously → building a **harness** (`scratchpad/stsharness`) that loads the
> client's own `StsConnLib64.MT.dll` and drives its real login against our server:
> `InitializeStsConnLib()` + `CreateStsConn(config)` WORK (returns a live
> connection); still need to (1) find the connection's login-trigger method on
> `CStsConn` (vtable `0x180124208`) and (2) implement the config callback interface
> (host/account/password/result). All SRP RE is in
> `NU-deconstruct/StsConnLib64.MT.dll/login-protocol.md`.
> Two baked-in laws (No-NF, Privacy, guards green); PUBLIC `NU-deconstruct` DB.
> RUNNING STATE: our engine + standalone MariaDB UP on standard ports; frozen realm DOWN.

_Updated 2026-08-19 (ENCRYPTION GATE CLOSED). Read `ARCHITECTURE.md` first._

## The situation

A **standalone** clean-room WildStar (16042) server engine whose entire reason to
exist is escaping the AGPL-3.0. Built from the client, our data, and the running
realm as behavioral oracle. MIT, open to anyone. Designed for BOTH emulation
fidelity AND a production multiplayer realm. Zero NF source; two MIT primitives
(Arctium SRP/ARC4 - note ARC4 is no longer used for the packet channel).

## BUILT + PROVEN (all pushed)

- **Login (SRP6a)** proven end to end (9/9).
- **Wire codec** validated against REAL captured packets (opcode + guid + position).
- **Framing** pinned + confirmed live (u32 LE self-inclusive size + u16 LE opcode).
- **157 opcodes** pinned from a live two-way capture (41 C->S + 116 S->C, decrypted).
- **10 message models** validated on real bytes; entity-create POSITION decoded
  (3x float32 at bit 289, real world coords).
- **ENCRYPTION GATE CLOSED**: the packet cipher is Carbine's own (NOT ARC4) - a
  128-byte key table from an 8-byte seed via two multiply-chains, CFB-style XOR
  with an 8-byte feedback register + rotating key block. `PacketCrypt.cs`
  reproduces the real captured keystream BYTE-FOR-BYTE (13/13). Seed = static
  build key **0xD283F5B34A8DC685**.
- **384 client tables** typed; names every creature. Reads real accounts (authdb).
- **World simulation**: entity/grid/vision/movement/aggro/combat; all 2,729 worlds
  resident at once (~98 MB); Arcterra runs (1,755 creatures, 600 ticks, zero NaN).
- **Content**: 263,756 spawns loaded (NOTE: inherited the frozen realm's current
  corruption - dupes, over-population, faction scramble; clean re-export = task #46).
- **Host + deploy**: runnable, boots as NexusUnleashed with our MotD; self-contained
  linux-x64 ELF + systemd.

## THE ROAD (task #48 = NORTH STAR: operator stands in the world on our engine)

DONE: crypto/login, wire codec, framing, protocol capture, message models,
container framing, **cipher SOLVED (two-phase keying, decrypts the whole world
stream)**. NEXT: the handshake payloads — 0x058F client hello / token → sessionKey
lookup → character list → character select → world entry → the client renders.

### Container framing wired; cipher partially reproduced (2026-08-19, this session)

The world channel's real structure was decoded byte-for-byte from our own login
capture and built into the engine:

- **`0x03DC` (S→C) / `0x0244` (C→S) are packed containers**:
  `[u32 innerLen self-inclusive][encrypted inner]`, inner = `[u16 op][body]`,
  enciphered with the build-seeded `PacketCrypt`. The auth channel (port 23115)
  is CLEAR direct frames; the world channel (24000) is the encrypted container.
- `Network/WorldPacket.cs` encodes/decodes it; `GameSession.Crypt` +
  container-aware dispatch + `SendGameMessageAsync` wire it into the transport;
  `GameServer(worldChannel:true)` seeds each session; `Realm/WorldHandshake.cs`
  sends the `0x0003` hello on connect and routes the client's login opcodes.
- **Proven (22/22 protocol):** DecodeContainer(real ServerHello) → inner `0x0003`
  + exact body; EncodeServer reproduces the captured wire byte-for-byte **for the
  first message**. Framing spec: `spec/protocol/containers.md`.

### CIPHER SOLVED: two-phase keying (2026-08-19)

The cipher is fully cracked and wired. It is **stateless-fixed-key**, with TWO
phases per connection:
- **auth key** `GetKeyFromAuthBuildAndMessage()` = `0xD283F5B34A8DC685` (a build
  constant) — the pre-login hello.
- **world key** `GetKeyFromTicket(sessionKey)` (folds the 16-byte SRP session
  key) — every message after login. Re-keyed implicitly at login; no key on wire.

**Proven byte-for-byte on the real capture:** recovered the full 128-byte world
key table from one known-plaintext world message (128/128, zero conflicts), it
rebuilds exactly from a keyInteger, and it decrypts the whole world-entry stream
(`0x0988` self-decrypts; the rest decode to `0x098B`, `0x0981` → the 251-id list,
…). Test 28/28. `PacketCrypt.GetKeyFromTicket` + `GameSession.RekeyForWorld` are
wired; `WorldHandshake` opens on the auth key and re-keys on the token hello.

**The earlier "stateful, msg #0 only" scare was an error:** the 12 "identical
hello" frames were actually 12 *different* 49-byte world messages under the world
key, not the hello under a moving register. Verify message identity before
concluding about a cipher.

### End-to-end loopback PROVEN over a real socket (2026-08-19)

`test/NexusUnleashed.Realm.Tests/LoopbackWorldEntry.cs`: a synthetic client drives
the real `GameServer` + `WorldHandshake` over TCP — connect → `0x0003` hello (auth
key) → send `0x058F` → server re-keys → `0x0981` world-init (world key) → verify
251 ids. **5/5.** The full two-phase encrypted handshake works end to end,
self-contained; the real 16042 client just replaces the synthetic one. Next: pin
the world-entry payloads (`0x0988`/`0x098B`/`0x0117`/`0x0262`) so the engine
generates the live player's world, then swap in the real client for the final
render (the one step that needs the operator's machine).

## The capture pipeline + facts (for the next session)

- Our own diagnostics tap: `packetdump=1` in the realm's `monitor.conf` logs every
  message opcode+bytes (C->S after client crypto, S->C before encryption).
  `packet-key.log` (via RecordKey in OnAccept) logs the static crypt seed.
- **Captures preserved (local, gitignored - session data): `realm-source/captures/`**
  (capture-session1-cs.log, capture-session2.log = 67,846 msgs both directions).
- `CaptureAnalyzer` (tool) turns a dump into an opcode inventory.
- **The cipher (facts)**: seed 0xD283F5B34A8DC685; SeedInitial 8182381946860333969;
  Multiplier 2860486313; LengthSeed 2860486314. Real keystream position 0:
  cf0c0e97c85f02238ce856b6f60d9b1d84466f01e710339191612a4284105ff8.
  `GetKeyFromAuthBuildAndMessage() = 606559840449654397 * 2860486313`.

## Frozen realm deployment state (deployed by us, 2026-08-19)

- Network.dll on Auth/World/STS carries the packet-dump + full-duplex + key-log tap
  (SHA ec177982). Old DLLs backed up as .bak-*.
- monitor.conf: packetdump=1, **sweeponboot=0** (disabled to avoid the 1,767-map
  shutdown bog), zone=3335, visibility=1, postrace=1, sprintbit=0x100, matchsolo=1.
- The realm bogs down / can't cleanly shut down with sweeponboot=1 (1,767 maps) -
  a real bug the clean engine fixes (graceful shutdown + concurrency).
