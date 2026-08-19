# Spec: the world-entry message sequence (the script to render the world)

**Status: ORDER PINNED from the capture — payloads pinned message-by-message.**

This is the exact ordered sequence of server→client messages the client received
at world entry, extracted from our own decrypted capture (session 2, the
`06:43:16`–`06:43:26` window, first-occurrence order). Every entry rode inside an
encrypted `0x03DC` container (`spec/protocol/containers.md`). Reproducing this
sequence — with each payload — is what makes the client render the world and the
operator stand in it (task #48). It is our own realm's captured output: bytes the
client demonstrably accepted, so reproduction is guaranteed-correct on the wire.

## The preamble (one-shot, in order)

| # | opcode | len | count | role (inferred from size/position) | status |
|---|---|---|---|---|---|
| 1 | `0x0988` | 5381 | 1 | world-entry payload (leads the sequence) | payload TODO |
| 2 | `0x098B` | 3152–19937 | 116 | world/zone state blob (streamed, per-grid) | payload TODO |
| 3 | `0x0987` | 2 | 1 | small marker | payload TODO |
| 4 | `0x0966` | 24 | 1 | | payload TODO |
| 5 | **`0x0981`** | 1010 | 1 | world-init id list (u32 count + ids) | **DONE — `ServerWorldInit`, byte-for-byte** |
| 6 | `0x0968` | 198 | 1 | | payload TODO |
| 7 | `0x097F` | 3 | 1 | small marker | payload TODO |
| 8 | `0x0036` | 6 | 1 | | payload TODO |
| 9 | `0x0117` | 833 | 1 | likely player/self character block (single, large) | payload TODO |
| 10 | `0x00AD` | 24 | | | payload TODO |
| 11 | `0x00FE` | 6 | | | payload TODO |
| 12 | **`0x0262`** | 270–2416 | 1068 | **entity-create stream begins** (spawns the world) | header + position DONE; body task #47 |

After `0x0262` the steady-state world stream follows (`0x0935` position heartbeat,
`0x0355` small updates, `0x0638` movement/spline, buffs, values…), already modeled.

## How to build it (the oracle loop)

1. The client connects to the world channel and receives our `0x0003` hello
   (DONE — `WorldHandshake`).
2. The client sends `0x058F` (its hello / realm-enter, token-bearing) and the
   short follow-ups `0x07E0`/`0x038C`/`0x082D`/`0x0000` (handlers log them today).
3. The server replies with **character list → select → this world-entry
   sequence**. Each message here is pinned by decoding its captured payload
   (the bytes are in `realm-source/captures/`, decrypted) and adding a
   `Server*` model with a Build that reproduces the captured bytes, exactly as
   `ServerWorldInit` (0x0981) was done.
4. Point the real client at our engine, watch what it renders / rejects, fix from
   the capture, repeat — until it stands in the world.

## Next payloads to pin (priority)

- `0x0117` (833B, one-shot) — the single large self/player block; likely the
  character the player is entering as. High value: the client needs "who am I."
- `0x0988` (5381B) — the world-entry payload that leads the sequence.
- `0x0262` body (task #47) — creatureId/faction/display, so spawns are the right
  creatures, not just positioned blanks.

All three are byte-present in the capture; each is a reproduce-from-capture model.
