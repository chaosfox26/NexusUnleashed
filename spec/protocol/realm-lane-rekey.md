# Spec: the realm-lane re-key (the C->S key changes after 0x058F)

**Status: SOLVED and LIVE (2026-08-20). The create packet now decrypts byte-exact
("Staatha Skyclear" read in plaintext) and characters persist.**

## The bug this fixes
The realm connection (the socket the client opens to `world_port` after `0x03db`)
starts on the auth key (`WorldChannelSeed = 0xD283F5B34A8DC685`). The client's first
message on that socket, **`0x058F` (realm-enter)**, is the LAST one ciphered with the
auth key. **Immediately after sending `0x058F`, the client re-keys its channel to a
fixed realm-lane key** and ciphers everything after (the char-create bundle, delete,
enter) with it. Our server kept using the auth key, so every post-`0x058F` C->S
message decrypted to garbage — the create opcode read as `0x5CD5` one session and
`0xC1ED` the next (same click, wrong key, random-looking result). `0x5CD5` was a
ghost; the real create opcode is `0x025C`.

## The realm-lane key
```
RealmLaneKey = 0x9A868DE642EF9906
```
**Provenance (no NF):** recovered live from the client's own cipher object via a
narrow Frida hook on the encrypt routine (`WS+0xC2D10`) — the 16-qword key table at
`obj+0x28` and the register at `obj+0xa8`. Inverting `key[0]` through our key
expansion (`seed = key[0] * inv(0xAA7F8EA9) - 0x718DA9074F2DEB91`) yields this seed,
and re-running our `PacketCrypt(seed)` reproduces the client's key table AND register
**byte-for-byte**. Two separate sessions (different cipher-object addresses) produced
the identical table, so it is a fixed constant, not session-derived. Confirmed
offline: the session-B create ciphertext decrypts to structured data (sub-message
headers `0x025C`/`0x025B`) only under this key.

This proves our cipher ALGORITHM was always correct; only the SEED was wrong.

## The fix (implemented)
`crypto/packet_crypt.h` — `RealmLaneKey` constant.
`realm/world_handshake.cpp` `RegisterRealmConnection` — on receiving `0x058F`, re-key
the session cipher: `s.crypt.emplace(net::WorldPacket::RealmLaneKey)`. (The `0x058F`
body itself was already decoded with the auth key before the handler runs.) Every
message after that — both C->S decode and S->C encode on this socket — uses
RealmLaneKey.

## Verified live
- Post-fix, the intermediate messages decode to REAL opcodes (`0x07E0`, `0x038C`,
  `0x0000`, `0x0352`) instead of garbage.
- The create bundle decodes at `0x025C` with a readable name.
- The character persists to characterdb and appears in the client's character list
  on the next connect (operator saw "Staatha Skyclear" at char-select).

## Known-good direction facts
- S->C and C->S on the realm lane both use RealmLaneKey after `0x058F` (the client
  accepted our RealmLaneKey-ciphered char list — it showed the new character).
- Container opcode stays `0x76` (ServerContainer) S->C, `0x0244` C->S.

## Client send opcodes observed on the realm lane (post-058F)
| opcode | dir | meaning |
|---|---|---|
| `0x058F` | C->S | realm-enter (triggers the re-key) |
| `0x025C` | C->S | character create (bundle; wraps sub-msg `0x025B`) |
| `0x0352` | C->S | character delete/select (body = u64 charId) |
| `0x0241` | C->S | periodic keepalive (0 body) |
| `0x07E0`,`0x038C`,`0x0000` | C->S | small follow-ups after realm-enter |

Server reply opcodes (from the client's char-select dispatcher `sub_140020EA0`):
`0x0117` list, `0x00DC` create-result, `0x00E6` delete, `0x012E` select-fail.
