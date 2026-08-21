# Spec: the world-entry message sequence (the script to render the world)

## PROVENANCE DECISION 2026-08-20: THE REPLAY IS RETIRED — WORLD ENTRY IS BUILT FROM SCRATCH
The `world-entry-replay.bin` approach — replaying server->client bytes captured from
`realm-source/captures/` (the AGPL/NexusForever-lineage realm-portable engine's output) — is
**NF reference-poison** by this project's own rule (`CONTINUE.md` §2) and has been **removed**:
the `.bin` is deleted, and the loader + `WorldEntrySequence` + the `0x07DD`-replay handler are
gone from `main.cpp` / `world_handshake.*`. It proved the client *can* be driven into the zone,
nothing more.

**World entry is now built by hand, like everything else in this engine:** each server->client
message's wire format is reverse-engineered from the **client's own deserializer** (the `Read`
functions in WildStar64.exe) and the bytes are **generated per-character from our own DB/world
data** — zero captures, zero NF. Client->server bytes (the `0x07DD` trigger, `0x058F`, `0x038C`
follow-ups) remain clean (the client's own) and stay as observed facts. The tables below record
the observed opcode ORDER and SIZES (facts about what the client consumes); the PAYLOADS are all
`TODO — derive from client + generate`, none carried over from the replay.

_(Everything below is the observed sequence shape — the build target — not implemented bytes.)_

**Still stuck on the loading screen (the last mile):** the burst is truncated at the first
heartbeat and the server ignores the client's `0x038C` movement, so the client never gets the
"load complete" it needs to drop into the zone. Next: (a) extend the replay past the first
heartbeat / keep the steady-state stream flowing, and (b) answer the client's `0x038C`
movement (echo/broadcast) so the world-ready handshake completes. The world stream is present
and accepted; this is generalization, not a new wall.

Extractor: `<scratch>/extract-world-entry.py`. The captured session's player is a
DIFFERENT character than the one entering (id 22), so the replayed player block is not yet
generated per-character — that generalization is also pending.

## THE TRIGGER (pinned live 2026-08-20)
"Enter Game" on a character at char-select sends, on the realm lane (RealmLaneKey,
see realm-lane-rekey.md), **C->S opcode `0x07DD`, body = u64 characterId** (observed:
`16 00 00 00 00 00 00 00` = char 22). The server must answer with the world-entry
sequence below. This is the last unbuilt wall to "standing in the world". Note: the
captured sequence rode a session whose reconnect used `0x058F`; the char-select enter
uses `0x07DD` — same downstream world stream. Open question to resolve on first test:
does the world stream stay on RealmLaneKey, or does 0x07DD trigger a further re-key to
a session world key? (Try RealmLaneKey first; the client's DEC hook confirms.)


**Status: ORDER PINNED; ALL PAYLOADS NOW DECRYPTABLE (cipher solved) — pinned
message-by-message.**

## Confirmed minimal flow (session 2, a realm-enter straight into the world)

The cleanest observed path — NO character-select exchange (a reconnect/enter with
the character already chosen):

```
S->C 0x0003              server hello           [AUTH key]
C->S 0x058F              client realm-enter (token/char)   [client re-keys after]
   (server re-keys to the WORLD key = GetKeyFromTicket(sessionKey) here)
C->S 0x07E0, 0x038C, 0x082D   small client follow-ups
S->C 0x0988              world-entry payload    [WORLD key] --- from here on
S->C 0x098B x many       zone/world state blobs
S->C 0x0981              world-init id list (DONE: ServerWorldInit)
S->C 0x0117              player self block (who/where am I)
S->C 0x0262 x many       entity-create stream (spawns the world)
```

Every S->C payload above is now recoverable in plaintext from the capture (the
world key decrypts the whole stream), so each is a reproduce-then-generalize
model. The re-key point is wired (`GameSession.RekeyForWorld`); the remaining work
is pinning each payload's fields so the engine GENERATES them for a live session
(the player's own guid/position/character), not just replays the captured one.

---

**(original notes below)**

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
