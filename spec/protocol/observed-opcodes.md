# Spec: observed opcodes (live capture, 2026-08-19)

**Status: client->server PINNED from a live capture; server->client pending
the full-duplex tap (staged).**

## Provenance

Captured from the running realm (the oracle) via our own diagnostics tap
(`packetdump=1`), which records each message's opcode + bytes AFTER the
client's crypto on receive - so these are the true decrypted client->server
opcodes, a protocol fact defined by Carbine's client. No emulator source was
read. Only opcode numbers, directions, counts, and payload-length ranges are
recorded here; raw payloads are kept out of the public repo because the login
messages carry session-specific auth material.

The framing matches `frame.md`: each message leads with its u16 LE opcode
(e.g. 0x0637 -> payload begins `3706...`).

## Client -> Server opcodes (41 distinct, one play session)

| opcode | count | len range | note |
|---|---|---|---|
| 0x0000 | 1 | 3..3 | State (also seen in gaps.log) |
| 0x0097 | 1 | 10..10 |  |
| 0x00BF | 1 | 3..3 |  |
| 0x00C0 | 1 | 2..2 |  |
| 0x00D5 | 2 | 5..5 |  |
| 0x00DE | 2 | 3..3 |  |
| 0x00F2 | 2 | 4..4 |  |
| 0x012B | 2 | 10..10 |  |
| 0x014F | 5 | 11..11 |  |
| 0x017E | 8 | 3..3 |  |
| 0x0182 | 3 | 13..13 |  |
| 0x0185 | 38 | 6..6 |  |
| 0x018F | 2 | 2..2 |  |
| 0x01AD | 5 | 2..2 |  |
| 0x023C | 86 | 38..38 |  |
| 0x023D | 11 | 71..93 |  |
| 0x023E | 4 | 34..34 |  |
| 0x023F | 8 | 18..18 |  |
| 0x0240 | 2 | 30..30 |  |
| 0x0244 | 2309 | 8..421 | packed/encrypted wrapper (carries inner msgs) |
| 0x0269 | 2 | 2..2 |  |
| 0x0356 | 1 | 2..2 |  |
| 0x035B | 2 | 9..9 |  |
| 0x037E | 3 | 12..12 |  |
| 0x038C | 2304 | 9..415 | unwrapped inner (movement-class) |
| 0x03EC | 2 | 2..2 |  |
| 0x03ED | 4 | 2..2 |  |
| 0x04DB | 352 | 9..9 | frequent action (target/cast-class) |
| 0x0570 | 1 | 10..10 |  |
| 0x058F | 1 | 43..43 |  |
| 0x0635 | 2 | 6..6 |  |
| 0x0637 | 1711 | 22..408 | movement/position stream (busiest) |
| 0x063A | 17 | 6..6 |  |
| 0x063B | 2 | 6..6 |  |
| 0x071D | 1 | 26..26 |  |
| 0x0720 | 4 | 4..4 |  |
| 0x07CC | 14 | 6..6 |  |
| 0x07DD | 2 | 10..10 |  |
| 0x07E0 | 2 | 2..2 |  |
| 0x07EA | 2 | 7..7 |  |
| 0x082D | 1 | 4..4 |  |

## Server -> Client

All server messages are encrypted under the `0x03DC` wrapper at the point the
current receive/flush tap sees them, so only the wrapper is visible so far.
The full-duplex tap (records the real opcode + body BEFORE encryption in
`EnqueueMessageEncrypted`) is built and staged; it lands on the next realm
restart and will pin the server->client half (entity spawn, world state, etc.).
