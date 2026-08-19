# Spec: the packet cipher keying — SOLVED (two-phase)

**Status: SOLVED (2026-08-19). The cipher is stateless-fixed-key; a connection
uses TWO keys (auth, then the login ticket key). Confirmed against the real world
stream, byte-for-byte.**

This file first (mistakenly) reported the cipher as "stateful, only msg #0
reproduced." That was WRONG, and the story of the mistake is instructive, so it
is kept below the answer.

## The answer

The cipher (`PacketCrypt`) is **stateless per message** — each Encrypt/Decrypt
starts from the same key table + register. One `PacketCrypt` instance serves a
whole phase. A connection has **two phases, two keyIntegers**:

| phase | keyInteger | when |
|---|---|---|
| **auth** | `GetKeyFromAuthBuildAndMessage()` = `606559840449654397 * 2860486313` = **`0xD283F5B34A8DC685`** (a build constant) | connection open → the pre-login hello |
| **world** | `GetKeyFromTicket(sessionKey)` — fold the 16-byte SRP session key through the multiply chain, add the auth constant, ×`Multiplier` | after login; every world message |

`GetKeyFromTicket`:
```
v = SeedInitial (8182381946860333969)
for each of the 16 session-key bytes b:  v = (v + b) * Multiplier
return (v + GetKeyFromAuthBuildAndMessage()) * Multiplier      // all mod 2^64
```

Both ends derive the identical world key from the shared SRP session key, so no
key material is ever sent — the re-key is implicit at login.

## Proof (real capture, byte-for-byte)

- **Auth key**: decrypts the first connection frame → inner `0x0003` hello, exact
  body; re-encrypt reproduces the captured wire. (`containers.md`.)
- **World key**: recovered the full 128-byte world key table from ONE known
  plaintext world message via `key[block+k] = plain[i] ^ cipher[i] ^ cipher[i-8]`
  — **128/128 bytes, zero conflicts** (self-consistent ⇒ pairing correct ⇒ cipher
  is stateless-fixed-key). That table **rebuilds exactly** from a keyInteger
  (`0x4888DCE5CA507060` for the captured session), and it decrypts the whole
  world-entry stream: `0x0988` self-decrypts exactly, the following wrappers
  decode to `0x098B`, etc. Test: `test/NexusUnleashed.Protocol.Tests` (28/28).

## Wired

`PacketCrypt.GetKeyFromAuthBuildAndMessage()` / `GetKeyFromTicket(sessionKey)`
(clean facts). `GameSession.RekeyForWorld(sessionKey)` switches the channel after
login. `WorldHandshake` opens on the auth key (hello) and re-keys on the client's
token hello. The channel is now genuinely functional both directions.

## The mistake (kept as a lesson)

The wrong "stateful" reading came from assuming all 49-byte (`len=53` wrapper)
frames were the identical `0x0003` hello. They were NOT: only the FIRST per
connection is the hello (auth key); the rest are *different* 49-byte world
messages under the *world* key, which of course don't decode with the auth key —
producing 12 "different ciphertexts for the same plaintext" that were really 12
different messages. Decrypting each with its correct key resolves everything.
Lesson: verify the message identity before concluding about the cipher; a wrong
plaintext assumption looked exactly like a stateful cipher.
