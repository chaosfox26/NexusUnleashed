# Spec: message framing

**Status: UNPINNED — shape authored, header widths await an oracle capture.**

## What is known (authored, final)

A WildStar message on the wire is a size-prefixed envelope carrying an opcode
and a bit-packed payload. This shape is a protocol fact and is implemented in
`GamePacketFrame` / `GameSession`:

```
[ size field ][ opcode field ][ bit-packed payload ... ]
```

## What is UNPINNED (the only thing awaiting evidence)

| constant | placeholder | how it gets pinned |
|---|---|---|
| `SizeFieldBits` | 32 | capture one frame off the oracle (the frozen NexusUnleashed realm) at login; measure the length prefix width and endianness |
| `OpcodeFieldBits` | 16 | same capture; opcodes are enumerated in the client and our datamine |
| size semantics | "opcode field + payload bytes" | confirm whether size counts itself, the opcode, or payload-only |

## How it will be pinned (the clean method)

The behavioral oracle is the source, **not** NF. Procedure:

1. Point a capture at the frozen realm's auth/world port while a client connects.
2. Read the first bytes of the first server→client frame; the length prefix is
   self-evident from the total frame size.
3. Record the measured widths here, flip the constants, and delete the UNPINNED
   markers. The provenance-ledger entry names the capture, not any source code.

Until then the constants are honest placeholders and the code says so. Nothing
ships to a "pinned" state on a guess.
