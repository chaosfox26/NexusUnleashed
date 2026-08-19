# Spec: the packet cipher's per-message state (the true remaining gate)

**Status: OPEN — the cipher reproduces message #0 byte-for-byte, but its
per-message state evolution across a connection is NOT yet reproduced.**

This corrects an earlier over-claim. "Encryption gate closed, byte-for-byte
(13/13)" was validated only against the **first** message on the connection (the
`0x0003` hello). It does not generalize: the live multi-message stream is not yet
decryptable with `PacketCrypt` as written.

## What is proven (stands)

- **Container framing** (`spec/protocol/containers.md`): `0x03DC`/`0x0244`,
  `[u32 innerLen][cipher]`. Correct.
- **Cipher algorithm + key table**: the 128-byte key from the multiply-chain, the
  CFB byte loop, and the length-derived block counter. For message #0 the decrypt
  yields `0x0003` + exact body, and the encrypt reproduces the captured ciphertext
  byte-for-byte, both directions.

## The proof that it is stateful (not stateless-per-message)

The `0x0003` hello has an **identical 49-byte plaintext** every time it is sent
(`0300aa3e0000…`). Captured 12 times in one session, it produced **12 DISTINCT
ciphertexts**. A stateless cipher would produce one. So the keystream depends on
connection state / message sequence, not on the plaintext alone. (Analysis:
`realm-source/captures/`; the 12 wrappers are byte-distinct.)

## What the evidence says about the state

For CFB, the first 8 cipher bytes of a known-plaintext message recover that
message's **starting register**: `reg[7-k] = cipher[k] ^ plain[k] ^ key[block0+k]`.
Recovered from the 12 hellos:

- The **first** message of the connection (session 1, msg #0) recovers the
  **static register `a`** (`0x7D546D1D1994C849`) exactly — confirming msg #0 uses
  the pristine build-derived state. This is why `PacketCrypt` works for it.
- **Later** hellos recover DIFFERENT registers (session 2 cluster
  ~`0xE3EF__C2486F____`; session 1 later ~`0x12C82B8A…`). The register evolves.

## Models tested and REJECTED (do not re-try blind)

Decrypting the session-1 S→C `0x03DC` stream from msg #0 (the true connection
start), each fails at msg #1 while msg #0 stays correct:

1. **Stateless per message** (register reset to `a` each msg) — msg #1 garbage.
2. **Continuous CFB feedback** (carry the working `fb` across messages, counter
   reset per length) — msg #1 garbage.
3. **Continuous counter** variants (carry the block counter) — msg #0 garbage too.
4. **Advance the multiply-chain per message** (register = next `a`, or next `b`)
   — msg #1 sometimes plausible (`0x0396`) but msg #2+ garbage.

## Leading hypotheses (for the next attack, in order)

1. **Shared full-duplex state**: one cipher state advanced by BOTH directions'
   bytes. The S→C-only continuous decrypt fails at msg #1 because the client's
   messages between msg #0 and msg #1 (`0x058F`/`0x07E0`/`0x038C`/`0x0000`) also
   advance the register. Test: reconstruct the exact interleaved wire byte order
   (C→S cipher is in the `0x0244` wrappers) and carry one register through all of
   it. This is the most likely resolution.
2. **Missing S→C messages** in `capture-session1-cs.log` (if the tap dropped some
   S→C frames, the register desyncs). Cross-check S→C frame counts against a
   second tap.
3. **Per-message re-key** via `GetKeyFromAuthBuildAndMessage()` (the ledger names
   this symbol): the key/register re-derived per message from (build seed, a
   per-message value — sequence number? opcode?). Would need the client binary's
   setup (source 1) or solving the sequence from the recovered registers.

## The cryptanalytic levers we hold (do not lose these)

- **12 known-plaintext hello pairs** (identical 49-byte plaintext, 12 ciphertexts)
  → 12 recovered starting registers spread across the connection.
- msg #0 register == static `a` (anchor).
- `capture-session1-cs.log` begins at the connection's msg #0 (the hello), so it
  is the stream to attack; `capture-session2.log` starts mid-connection (06:37).

## Impact on the road

The wired world channel sends a correct FIRST hello, but subsequent S→C messages
would be enciphered wrong until this is solved — so a real client would accept the
hello and then reject the stream. This is now the true task-#48 blocker, ahead of
character list / world entry. The framing, the message models, and the world
simulation are all ready to plug in the moment the cipher stream is reproduced.
