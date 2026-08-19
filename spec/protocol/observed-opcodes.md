# Spec: observed opcodes (live two-way capture, 2026-08-19)

**Status: 157 opcodes PINNED from a live full-duplex capture of the oracle —
41 client->server + 116 server->client, all decrypted.**

## Provenance

Captured from the running realm (the behavioral oracle) via our own
diagnostics tap (`packetdump=1`). Client->server is recorded after the
client's crypto on receive; server->client is recorded BEFORE encryption in
`EnqueueMessageEncrypted` - so both directions are the true decrypted
opcodes, a protocol fact defined by Carbine's client. No emulator source was
read. Only opcode numbers, directions, counts, and payload-length ranges are
public here; raw payloads are kept local (login/world blobs carry
session-specific data). Framing per `frame.md` (u16 LE opcode leads each msg).

Role notes are INFERRED from frequency + length signatures, to be confirmed
by payload analysis; they are hints, not pinned semantics.

## Server -> Client (116 distinct)

| opcode | count | len range | inferred role |
|---|---|---|---|
| 0x0003 | 45 | 49..49 | AuthHello (pinned) |
| 0x0036 | 1 | 6..6 |  |
| 0x0061 | 3 | 2..2 |  |
| 0x00AD | 3 | 24..24 |  |
| 0x00AE | 3 | 6..78 |  |
| 0x00AF | 2 | 78..78 |  |
| 0x00D9 | 3 | 6..6 |  |
| 0x00DD | 7 | 19..19 |  |
| 0x00E0 | 3 | 10..16 |  |
| 0x00E2 | 1 | 8..8 |  |
| 0x00F1 | 3 | 12..12 |  |
| 0x00FE | 3 | 6..6 |  |
| 0x0100 | 3 | 11..11 |  |
| 0x010E | 3 | 22..22 |  |
| 0x0111 | 115 | 100..100 | fixed stat/vitals block (100B) |
| 0x0117 | 1 | 833..833 |  |
| 0x012E | 3 | 70..70 |  |
| 0x0169 | 3 | 10..10 |  |
| 0x0171 | 3 | 10..10 |  |
| 0x017F | 6 | 15..15 |  |
| 0x019B | 3 | 10..10 |  |
| 0x019D | 12 | 467..467 |  |
| 0x019F | 6 | 3..3 |  |
| 0x01A0 | 3 | 469..469 |  |
| 0x01A3 | 12 | 4..4 |  |
| 0x01AC | 15 | 22..22 |  |
| 0x01B3 | 1 | 24..24 |  |
| 0x01B4 | 6 | 236..236 |  |
| 0x01BC | 2 | 25..25 |  |
| 0x01C8 | 9 | 89..113 |  |
| 0x0210 | 1 | 2..2 |  |
| 0x0211 | 1 | 18..18 |  |
| 0x0212 | 1 | 18..18 |  |
| 0x0215 | 2 | 19..19 |  |
| 0x0216 | 2 | 16..16 |  |
| 0x0218 | 2 | 20..20 |  |
| 0x021C | 5 | 29..29 |  |
| 0x0220 | 1 | 11..11 |  |
| 0x0228 | 3 | 46..46 |  |
| 0x022A | 8 | 9..9 |  |
| 0x022B | 1 | 7..7 |  |
| 0x022E | 1 | 7..7 |  |
| 0x0230 | 3 | 14..14 |  |
| 0x0232 | 2 | 12..12 |  |
| 0x0247 | 15 | 14..14 |  |
| 0x0252 | 3 | 14..14 |  |
| 0x025A | 3 | 6..6 |  |
| 0x025E | 3 | 2046..2437 |  |
| 0x0262 | 1068 | 270..2416 | entity create/spawn (variable per entity) |
| 0x026A | 1 | 62..62 |  |
| 0x0355 | 911 | 7..7 | frequent small update (7B) |
| 0x0357 | 4 | 7..7 |  |
| 0x035C | 7 | 12..12 |  |
| 0x035F | 3 | 22..77 |  |
| 0x0361 | 13 | 12..12 |  |
| 0x039F | 1 | 10..10 |  |
| 0x03A3 | 1 | 6..6 |  |
| 0x03A6 | 1 | 5..5 |  |
| 0x03BA | 1 | 4..4 |  |
| 0x03BE | 1 | 4..4 |  |
| 0x0497 | 1 | 10..10 |  |
| 0x0507 | 3 | 6..6 |  |
| 0x056F | 1 | 14..14 |  |
| 0x05A3 | 3 | 7..7 |  |
| 0x05E3 | 1 | 6..6 |  |
| 0x0636 | 5 | 11..11 |  |
| 0x0638 | 7203 | 47..759 | movement command / spline broadcast |
| 0x0639 | 2 | 2..2 |  |
| 0x0640 | 3 | 6..6 |  |
| 0x064C | 1 | 6..6 |  |
| 0x064D | 2 | 6..6 |  |
| 0x06BC | 3 | 23..23 |  |
| 0x0723 | 61 | 6..6 |  |
| 0x07CA | 7 | 523..523 |  |
| 0x07CD | 24 | 20..20 |  |
| 0x07F2 | 8 | 55..136 |  |
| 0x07F4 | 383 | 58..370 |  |
| 0x07F9 | 12 | 12..12 |  |
| 0x07FC | 205 | 10..10 |  |
| 0x07FD | 16 | 32..32 |  |
| 0x07FE | 361 | 6..6 |  |
| 0x07FF | 349 | 41..93 |  |
| 0x0804 | 5 | 13..13 |  |
| 0x0811 | 24 | 14..14 |  |
| 0x0813 | 24 | 10..10 | spell buff remove (per notes) |
| 0x0845 | 3 | 14..14 |  |
| 0x0876 | 197 | 10..10 |  |
| 0x087F | 2 | 11..11 |  |
| 0x0880 | 2 | 15..15 |  |
| 0x088C | 34 | 11..11 |  |
| 0x089A | 327 | 7..7 |  |
| 0x08A5 | 17 | 51..51 |  |
| 0x08B8 | 3 | 7..7 |  |
| 0x0908 | 221 | 14..14 |  |
| 0x0909 | 71 | 46..46 |  |
| 0x090A | 194 | 14..14 |  |
| 0x0919 | 9 | 19..19 |  |
| 0x091B | 3 | 6..17 |  |
| 0x092C | 3 | 383..383 |  |
| 0x092F | 332 | 13..13 |  |
| 0x0935 | 14087 | 11..11 | entity position/move broadcast (heartbeat) |
| 0x0937 | 323 | 11..11 |  |
| 0x0938 | 1127 | 11..11 |  |
| 0x093A | 46 | 25..25 |  |
| 0x093C | 290 | 9..9 |  |
| 0x0966 | 1 | 24..24 |  |
| 0x0967 | 6 | 35..35 |  |
| 0x0968 | 1 | 198..198 |  |
| 0x096D | 1 | 6..6 |  |
| 0x096E | 1 | 27..27 |  |
| 0x097F | 1 | 3..3 |  |
| 0x0981 | 1 | 1010..1010 | character/world init (one-shot) |
| 0x0987 | 1 | 2..2 |  |
| 0x0988 | 1 | 5381..5381 | world-entry payload (one-shot) |
| 0x098B | 116 | 3152..19937 | world/zone state blob |

## Client -> Server (41 distinct)

| opcode | count | len range | inferred role |
|---|---|---|---|
| 0x0000 | 1 | 3..3 | State |
| 0x0097 | 27 | 10..10 |  |
| 0x00D5 | 3 | 5..5 |  |
| 0x00DE | 14 | 3..3 |  |
| 0x00F2 | 3 | 4..4 |  |
| 0x012B | 1 | 10..10 |  |
| 0x014F | 12 | 11..11 |  |
| 0x015A | 1 | 11..11 |  |
| 0x017E | 22 | 3..3 |  |
| 0x0185 | 81 | 6..6 |  |
| 0x018F | 1 | 2..2 |  |
| 0x01AD | 4 | 2..2 |  |
| 0x023C | 49 | 38..38 |  |
| 0x023D | 13 | 71..85 |  |
| 0x023E | 2 | 34..34 |  |
| 0x023F | 5 | 18..18 |  |
| 0x0240 | 4 | 30..30 |  |
| 0x0244 | 3644 | 8..505 | packed/encrypted wrapper |
| 0x0269 | 1 | 2..2 |  |
| 0x0356 | 4 | 2..2 |  |
| 0x035B | 6 | 9..9 |  |
| 0x035D | 2 | 6..6 |  |
| 0x038C | 3641 | 9..499 |  |
| 0x03EC | 1 | 2..2 |  |
| 0x03ED | 2 | 2..2 |  |
| 0x04DB | 460 | 9..9 | frequent action (target/cast-class) |
| 0x0570 | 1 | 10..10 |  |
| 0x058F | 1 | 43..43 |  |
| 0x0635 | 5 | 6..6 |  |
| 0x0637 | 2870 | 22..492 | movement/position stream |
| 0x063A | 9 | 6..6 |  |
| 0x063B | 3 | 6..6 |  |
| 0x071D | 1 | 26..26 |  |
| 0x0720 | 4 | 4..4 |  |
| 0x07CC | 7 | 6..6 |  |
| 0x07DD | 1 | 10..10 |  |
| 0x07E0 | 1 | 2..2 |  |
| 0x07EA | 9 | 7..7 |  |
| 0x0801 | 5 | 8..8 |  |
| 0x0805 | 8 | 11..11 |  |
| 0x082D | 1 | 4..4 |  |

## The wrapper

`S->C 0x03DC` still appears (the outgoing encrypted-envelope, dumped at the
wire by the flush hook); the real inner opcodes above come from the
pre-encryption hook. `C->S 0x0244` is the client-side packed wrapper carrying
inner messages, which the receive hook also unwraps.
