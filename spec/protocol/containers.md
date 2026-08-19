# Spec: the encrypted packed container (world channel)

**Status: FRAMING PINNED byte-for-byte; CIPHER SOLVED (two-phase keying).**

> **2026-08-19:** the container FRAMING is proven, and the cipher is now SOLVED —
> stateless-fixed-key with TWO phases: the auth key (`0xD283F5B34A8DC685`) for the
> hello, then a re-key to `GetKeyFromTicket(sessionKey)` for the world stream.
> Confirmed against the real world stream byte-for-byte. See
> `spec/protocol/cipher-state.md`. (An intermediate "stateful, msg #0 only" reading
> was an error — the later 49-byte frames were different messages under the world
> key, not the hello under a moving key.)

This is the structure that carries every game message on the world channel: the
world wraps each game message in a container and encrypts the inner message with
the phase-appropriate `PacketCrypt`.

## The two channels

| channel | port | envelope | encryption |
|---|---|---|---|
| **auth** | 23115 | direct `[u32 size][u16 opcode][body]` frames | **clear** — the auth hello `0x0003` was captured with a low-entropy structured body |
| **world** | 24000 | every message wrapped in a **packed container** | **encrypted** — inner message ciphered with `PacketCrypt(0xD283F5B34A8DC685)` |

Evidence (`spec/protocol/frame.md` raw captures):
- auth `35000000 0300 aa3e0000…` — opcode `0x0003`, clear.
- world `3b000000 dc03 35000000 1a57c0cb…` — opcode `0x03DC`, high-entropy payload.

## The container (world channel)

```
outer frame : [ u32 size ][ u16 containerOpcode ][ container payload ]
container   : [ u32 innerLen (self-inclusive) ][ encrypted inner ]
inner (dec) : [ u16 opcode ][ bit-packed body ]
```

- `containerOpcode` = **`0x03DC`** server→client, **`0x0244`** client→server.
- `innerLen` counts its own 4 bytes + the ciphertext (`innerLen = 4 + cipherLen`).
- The ciphertext is exactly `cipherLen` bytes; it decrypts (static seed) to the
  real game message `[u16 opcode][body]`.
- The cipher's length counter keys on the **inner message length** (opcode + body),
  i.e. `cipherLen`. Each container is enciphered independently from the static
  register (the stream does not carry state across messages — proven by the
  standalone decrypt of the first frame).

## Proof (real captured ServerHello, first S→C frame of session 1)

```
S->C 0x03DC payload = 35000000 1a57c0cbff79ba9c87080349bf63806df50021ea5e4a2918faa344ca85401d094f69e88d7762748aee15966790e91be068
  innerLen = 0x35 = 53  -> cipher = 49 bytes
  PacketCrypt(0xD283F5B34A8DC685).Decrypt(cipher) =
    0300 aa3e0000010000001500000000000000000000000000000000000b14332f0100000000000000000000000000000000
  inner opcode = 0x0003 (AuthHello); body EXACTLY matches the decrypted 0x0003
  seen post-decryption in session 2 (0300aa3e0000010000001500...).
```

`Encrypt` of that plaintext reproduces the captured ciphertext byte-for-byte.
Verified in `test/NexusUnleashed.Protocol.Tests` (real-wire). World-phase messages
use the ticket key and decrypt/encrypt the same way (`cipher-state.md`); a real
captured world message (`0x0981`) decrypts end-to-end through this codec.

## Client→server direction

Captured client containers (`0x0244`) at login were logged post-decryption by the
oracle's receive hook; the inner opcodes (`0x058F`, `0x07E0`, `0x038C`, `0x0000`)
are the true decrypted client messages. Because the cipher is symmetric and the
seed is static, the server decrypts inbound with the same `PacketCrypt`. The exact
client-side byte reproduction is confirmed live when a real client connects (the
oracle loop); the algorithm is identical to the proven S→C path.

## Login handshake order (world channel, from session 1)

```
S->C 0x03DC { 0x0003 }         server hello
C->S 0x0244 { 0x058F }         client hello / realm-enter (token-bearing)
C->S 0x0244 { 0x07E0 }
C->S 0x0244 { 0x038C }
C->S      0x082D               (small, unwrapped)
C->S      0x0000               State
S->C 0x03DC { … }  x many      character/world data + entity spawn stream
                               (0x0981 world init, 0x0988 world entry,
                                0x098B zone state, 0x0262 entity create …)
```

The road: implement the `0x0003` hello, accept the client's `0x058F` enter, then
stream the world-entry messages so the client renders. Each is pinned from the
capture as it is built.
