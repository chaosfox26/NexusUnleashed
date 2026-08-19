# Spec: the encryption gate (the road to "in the world")

**Status: CLOSED (2026-08-19) — framing + cipher both solved. See
`spec/protocol/cipher-state.md` and `containers.md`.**

> The gate is genuinely shut. **No ARC4, no key material on the wire.** The world
> channel wraps every game message in a packed container (`0x03DC` S→C / `0x0244`
> C→S) and enciphers the inner message with a stateless `PacketCrypt` under a
> TWO-PHASE key: the auth constant `0xD283F5B34A8DC685` for the hello, then
> `GetKeyFromTicket(sessionKey)` (from the SRP session key) for the world stream.
> Proven byte-for-byte on the real capture — the world key decrypts the whole
> world-entry stream. Wired: `WorldPacket`, `GameSession.Crypt` +
> `RekeyForWorld`, `WorldHandshake`. The text below is the original open-problem
> record (superseded).

---

**Status (historical): OPEN — the biggest remaining piece before a client reaches the world.**

Definition of done for the whole engine: the operator's real 16042 client
connects to THIS engine, authenticates, selects a character, and stands in the
world. The gate between "we speak the protocol" and that goal is the encrypted
session channel.

## What is known

- Login (STS + SRP6a) is implemented and proven; SRP yields a 64-byte session
  key (`SrpServer`).
- The world/auth game stream is **encrypted** (ARC4 - we have the primitive,
  MIT Arctium). Server->client game messages ride inside a `0x03DC` wrapper
  (`ServerRealmEncrypted` / `ServerAuthEncrypted`); the FIRST `0x03DC` (len 53,
  unencrypted) is the ServerHello that establishes the channel.
- The tap captured BOTH plaintext (pre-encryption, our send hook) and ciphertext
  (the `0x03DC` wrappers) - a known-plaintext lever.

## The unknown (the gate)

**How the ARC4 channel key is derived from the SRP session key** (+ any seed in
the ServerHello). This derivation is a client fact; it is NOT in the MIT Arctium
source (only ARC4 is), and NF's PacketCrypt is off-limits (AGPL expression).

Clean sources, in order of preference:
1. **The client** - it performs the identical derivation; extract the packet-
   crypto setup from the 16042 client binary (hard, but the authoritative fact).
2. **Capture cryptanalysis** - pair each plaintext message with its exact
   ciphertext (NOT by length alone: the `0x03DC` wrapper adds framing overhead,
   so ciphertext_len != plaintext_len; pairing must strip the wrapper header and
   match by sequence). Recover the keystream, then the key setup.
3. Reconstruct the ServerHello (0x03DC len 53) structure to find the seed the
   client mixes with the session key.

## The road after the gate

encrypted channel -> auth server handshake -> character list / select ->
world server entry (world-state blobs 0x0988/0x0981/0x098B + entity spawns
0x0262, position already decoded) -> the client renders the world. Built
iteratively with the client as oracle: point client at the clean engine, see
what it rejects, implement/fix from the capture, repeat.

## What is already done toward it

- 157 opcodes pinned; codec validated on real packets; 10 message models
  validated; entity-create POSITION decoded. The vocabulary is in hand; the
  handshake/crypto is the remaining climb.
