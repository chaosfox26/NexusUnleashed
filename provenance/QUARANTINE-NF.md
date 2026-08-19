# Quarantine: NF-derived material removed from the clean tree

Under the No-NF law (`provenance/NO-NF.md`, baked in 2026-08-19), anything whose
*derivation* was read from the NF-derived `recovered/` tree is tainted — even if
no NF text remains in the file. This records what was found in a self-audit of
this session's work and what was done about it. The goal: the clean tree contains
**nothing** sourced from NF, in text or in derivation.

## Found (2026-08-19 self-audit)

While solving the packet cipher earlier this session, two things were read from
`realm-source/recovered/NexusUnleashed.Cryptography/PacketCrypt.cs` (decompiled
NF) and used:

| item | what was tainted | status |
|---|---|---|
| **auth-key decomposition** | expressing the auth key as `606559840449654397 * Multiplier` (and the method name `GetKeyFromAuthBuildAndMessage`) — read from NF | **FIXED.** The auth key VALUE `0xD283F5B34A8DC685` is clean (observed at runtime on the wire; it reproduces the captured keystream). We now state that value directly as `PacketCrypt.AuthChannelKey`; the NF factoring and method name are gone. |
| **world-key derivation** | the `GetKeyFromTicket(sessionKey)` formula (fold the 16-byte session key through the multiply chain, add the auth const, ×Multiplier) — read from NF | **QUARANTINED.** Removed from shipping code. The world channel now takes a world keyInteger directly (`RekeyForWorld(ulong)`); the self-test uses a fixed dev key. |

## What stays (all clean — from YOUR captures, not NF)

- The cipher algorithm + the auth key VALUE — observed on the wire.
- The packed-container framing (0x03DC/0x0244) — decoded byte-for-byte from the
  capture.
- The 128-byte WORLD key TABLE — recovered from a known-plaintext world message
  in the capture (`key[b+k] = plain[i]^cipher[i]^cipher[i-8]`), pure cryptanalysis
  on your packets. This is why the world stream decrypts without any NF formula.
- All message models (0x0981 world-init, the server messages) — from the capture.

## Re-source plan (to lift the quarantine cleanly)

The world-key derivation (SRP session key → world keyInteger) must be obtained
from a CLEAN source before world entry ships to a live client:

1. **The 16042 client binary** (source #1) — the client computes the identical
   world key to decrypt; extract the derivation from Carbine's own crypto. This is
   the authoritative clean source.
2. **Cryptanalysis of a capture with a known session key** — if we capture a login
   where we control/observe the SRP session key, we can confirm the formula that
   maps it to the recovered world keyInteger, purely from your data.

Until then: the two-phase re-key MECHANISM is proven (auth key → world key over a
real socket), and the world channel works with a directly-supplied key. Only the
per-session derivation waits. Login (STS) does not need it.
