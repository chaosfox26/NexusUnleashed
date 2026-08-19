# Capture Analyzer — dump to protocol facts

Reads a `packet-dump.log` (produced by our own diagnostics tap on the running
oracle — `packetdump=1` in the realm's `monitor.conf`) and emits a clean
protocol-facts reference: per opcode, the direction(s), count, payload-length
distribution, and sample bytes.

```
NexusUnleashed.CaptureAnalyzer <packet-dump.log> [outDir]
```

Output: `opcode-inventory.tsv` (opcode, dir, count, minLen, maxLen, distinctLens,
samplePayload) plus a console summary of the busiest opcodes.

This is the clean bridge from a play-session capture to pinned message models:
the dump is observed wire bytes (a fact), this tool just aggregates them. No
emulator source is involved at any step. Because the dump taps the wire *before*
the realm's handlers, it captures every message the client sends — including
ones the realm doesn't implement — so a work-in-progress oracle does not limit
the protocol coverage.
