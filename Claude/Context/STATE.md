# NexusUnleashed Engine — State of the Build

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
concluding about a cipher. Real blocker is now just the handshake payloads
(0x058F token → sessionKey lookup → character list → world entry).

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
