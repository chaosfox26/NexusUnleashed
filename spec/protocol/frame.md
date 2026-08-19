# Spec: message framing

**Status: PINNED — measured against the behavioral oracle 2026-08-19.**

## What is known (authored, final)

A WildStar message on the wire is a size-prefixed envelope carrying an opcode
and a bit-packed payload:

```
[ u32 LE size ][ u16 LE opcode ][ bit-packed payload ... ]
```

## Pinned values (oracle capture)

Captured from the frozen NexusUnleashed realm (the oracle) 2026-08-19 by
connecting to its live ports and reading the first server→client frame. This
is a behavioral measurement of the wire, not a reading of any source.

| constant | value | evidence |
|---|---|---|
| `SizeFieldBits` | **32**, little-endian | auth port 23115 opened with `35 00 00 00` = 53, and exactly **53** bytes followed — the size counts the whole frame including its own 4 bytes. World port 24000: `3b 00 00 00` = 59, and 59 bytes followed. Two independent frames agree. |
| `OpcodeFieldBits` | **16**, little-endian | bytes 4–5 after the size: auth `03 00` = opcode 3; world `dc 03` = 988. Both are plausible server-hello opcodes and enumerated in the client. |
| size semantics | **self-inclusive** (counts the size field + opcode + payload) | the declared value equals the total received frame length exactly, on both ports. |

### Raw captures (kept for the record)

```
auth  :23115  53 bytes
  35 00 00 00  03 00  aa 3e 00 00 00 00 ...        size=53 opcode=3
world :24000  59 bytes
  3b 00 00 00  dc 03  35 00 00 00 1a 57 c0 cb ...  size=59 opcode=988
```

The world frame's payload is high-entropy after the opcode (`c0 cb ff 79 ba
9c ...`), consistent with the world server's encrypted/keyed channel; the auth
frame's payload is low-entropy and structured, consistent with a clear-text
auth hello. Payload interpretation per opcode is pinned message-by-message as
each is implemented (the opcode set is enumerated in the client and our
datamine).

## Note

The header framing is now final. What remains UNPINNED is *per-message payload
layout*, pinned one opcode at a time as messages are built — a separate axis
from the envelope, which this file governs and which is done.
