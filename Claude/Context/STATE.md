# NexusUnleashed Engine — State of the Build

_Updated 2026-08-19. The "where are we right now" file. Read `ARCHITECTURE.md`
(the constitution) first, then this._

---

## The one-paragraph situation

NexusUnleashed is a **clean-room WildStar (build 16042) server engine**, built
from Carbine's client, our own restoration data, and a running reference realm
as the behavioral oracle — owing no upstream license anything. It exists because
the prior engine descended from NexusForever (AGPL-3.0), and we chose to rebuild
rather than live under a license used as leverage. **MIT-licensed and openly
usable by anyone**, forked or not, no credit required (see README).

## Acceptance criteria (binding — the definition of done)

1. A real 16042 client **logs in** and reaches character select, then a world.
2. It **deploys as the private server on NU-Linux** the way the current engine
   does.
3. It is **as functional as the current engine** — behavioral parity with the
   frozen realm, indistinguishable to a player.

## What is BUILT and PROVEN (all pushed, all clean, zero NF)

| layer | project | status |
|---|---|---|
| Crypto | `Cryptography` | SRP6a/ARC4/Adler32 (MIT Arctium, attributed) + our RNG. **Full SRP login proven 9/9**: register→B→(A,M1)→verify, session keys agree, bad paths rejected. |
| Wire format | `Network` | Bit packer (12/12). **Framing PINNED off the oracle**: u32 LE self-inclusive size + u16 LE opcode (auth :23115, world :24000 captured). |
| STS login | `Sts` | Text-protocol parser (11/11) + server + **AuthFlow running real SRP**. Login works **over a live socket (7/7)**: client → token; wrong password rejected on the wire. Protocol pinned from the client's own `StsConnLib64.MT.dll`. |
| Client tables | `GameData` + `.Gen` + `.Generated` | `.tbl` reader + **code generator: all 384 client tables → typed C# records** (facts→generated). Compiles; core tables load (53,137 creatures / 66,383 spells / worlds incl. 990+3335 / 5,194 quests). |
| Accounts | `Database` | `DbAccountStore` over authdb (MySqlConnector, MIT). **Reads real SRP creds from the live authdb (5/5).** |
| World data | `Content` + `content/` | Native TSV format + **the whole restoration loaded: 263,756 spawns / 65 worlds / 8,059 patrols / 20,020 kit entries** (8/8, counts == live DB). |
| Server host | `Realm` | Runnable exe: boots STS + world listeners, DB or in-memory store by config. |

Provenance for every file: `provenance/LEDGER.md`.

## What is NOT done (the honest road)

- **XML body element names for the STS messages** — the only UNPINNED piece of
  login. SRP values currently ride as hex in `<Content>`; the flow, state
  machine, and crypto are real. One oracle capture pins the element names, then
  the (de)serialization swaps — nothing else changes.
- **World entry** — realm handoff (game token → world server), character list,
  character select, world enter. Opcodes come from the client (facts). NEXT.
- **The living world** — entity/map/movement/vision, spells, AI, combat.
- **The systems** — quests, items, groups, etc.
- **The parity harness** — drives both engines, diffs the wire. Skeleton only.
- **NU-Linux deployment packaging.**

## Next actions

1. Extract the opcode set from the client (WildStar64.exe / message defs) — the
   fact table the whole world-message layer keys on.
2. Realm handoff + character list + character select + world entry, each
   pinned/parity-checked against the oracle.
3. Living world → systems → parity → NU-Linux deploy.

## The laws (never violated)

- **Provenance Discipline** (`ARCHITECTURE.md §1`): every line from the client,
  our data, the oracle, or permissive code. Facts (opcodes, formats, behavior)
  are free; NF *expression* (text, translation, paraphrase, discretionary
  architecture) is never taken.
- **Openness Law** (`§1a`): everything documented and pushed to the public repo,
  in real time. A change not pushed is not done.
