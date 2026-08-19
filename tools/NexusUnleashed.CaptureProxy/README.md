# Capture Proxy — the protocol oracle tap

A **passive** man-in-the-middle between a real WildStar client and a running
reference realm (the behavioral oracle). It forwards every byte untouched and
logs each framed message's `(direction, opcode, length, payload preview)`.

This is how NexusUnleashed pins protocol facts **cleanly**: nobody reads an
emulator's source. The operator runs a real client through the proxy and plays;
we observe the wire. Watching bytes on a socket is a fact, not copyrightable
expression.

## Use

```
NexusUnleashed.CaptureProxy <listenPort> <targetHost> <targetPort> <logFile>
```

1. Start the oracle realm (its auth :23115 / world :24000).
2. Run the proxy, e.g. `... 29000 127.0.0.1 24000 world.log`.
3. Point the client's connection at `127.0.0.1:29000` (via the realm list host
   entry / config the launcher uses).
4. Play. `world.log` fills with the opcode stream, both directions.

The `u32 LE self-inclusive size + u16 LE opcode` envelope (PINNED,
`spec/protocol/frame.md`) is visible even when the payload is encrypted, so the
**opcode set and message cadence are captured regardless** of channel crypto —
enough to enumerate opcodes and pin message order. Clear-text stretches (the STS
auth exchange before SRP completes) also yield payloads.

## Proven

Frame parsing verified against a mock oracle: both S->C and C->S frames logged
with exact opcode + length; bytes forwarded verbatim.
