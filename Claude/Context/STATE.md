# NexusUnleashed Engine — State of the Build

_Written 2026-08-19. This is the "where are we right now" file. Read
`ARCHITECTURE.md` (the constitution) first, then this._

---

## The one-paragraph situation

NexusUnleashed is a **clean-room WildStar (build 16042) server engine**, built
from Carbine's client, our own restoration data, and a running reference realm
as the behavioral oracle — owing no upstream license anything. It exists because
the prior engine descended from NexusForever (AGPL-3.0), and we chose to rebuild
rather than live under a license used as leverage. The **restoration itself**
(263,756-entity world data, retail combat kits, all tools, all knowledge) is
already ours and free and carries over verbatim. This repo rebuilds only the
**engine** underneath it, clean.

## Acceptance criteria (binding — the definition of done)

1. A real 16042 client **logs in** and reaches character select, then a world.
2. It **deploys as the private server on NU-Linux** (the operator's Ubuntu VPS)
   the way the current engine does — one-command bring-up, systemd, the DBs.
3. It is **as functional as the current engine** — full behavioral parity with
   the frozen realm, indistinguishable to a player.

"It compiles" is not done. "It boots and a player can't tell the difference, on
the Linux box" is done.

## What is BUILT and PROVEN (all pushed, all clean, zero NF)

| layer | project | status |
|---|---|---|
| Crypto bootstrap | `NexusUnleashed.Cryptography` | SRP6a/ARC4/Adler32 from **Arctium (MIT, attributed)** + our own RNG. Builds. |
| Wire format | `NexusUnleashed.Network` | Bit-packed `PacketReader`/`PacketWriter` (**12/12 round-trip test PASS**), `GamePacketFrame`, `GameSession` (modern .NET Pipelines), `GameServer` (async acceptor + opcode table). Authored, zero Arctium/NF. |
| Client tables | `NexusUnleashed.GameData` | `GameTableReader` reads Carbine's `.tbl` from **our own datamine spec** — verified on real `Creature2.tbl`: **53,137 rows / 173 fields correct**. |
| Server host | `NexusUnleashed.Realm` | Runnable exe: config + logger + boots a listening `GameServer`. The engine is a *running server*, not just libraries. |

Provenance for every file is recorded in `provenance/LEDGER.md`; every entry
names a non-NF source (the client, our data, our datamine, or MIT Arctium).

## What is NOT done (the honest road)

- **The login handshake is UNPINNED.** The framing/crypto constants
  (`spec/protocol/frame.md`) need **one packet capture from a real client
  against the frozen realm (the oracle)** to fix widths/opcodes. This is the one
  operator-in-the-loop moment on the near path: a client connecting while the
  wire is watched. Everything up to it is buildable solo.
- **World entry, entity/map/movement/vision, spells, AI, combat, the systems** —
  the ~40–60K-line creative core, spec-first, each component parity-tested
  against the oracle. Not started.
- **The content loader** — consumes our TSV/SQL world data unchanged. Not started.
- **The parity harness** — drives both engines and diffs the wire. Skeleton only.
- **Deployment to NU-Linux** — packaging like the current install.sh. Not started.

## Scope, measured (provenance-audit of the frozen tree, 151,021 lines)

- **A-AUTHORED 9.1%** — ours, carries over (surfaces retargeted).
- **B-PORTED 13.8%** — fork code; becomes behavior specs from our ledgers.
- **C-LINEAGE + C-MODIFIED 77%** — the true rewrite; shrunk hard by generators
  (network models, GameTable models, enums, DB models are all facts → generated)
  and the MIT Arctium seed. True hand-written creative core ≈ 40–60K lines.

## The laws (never violated)

- **Provenance Discipline** (`ARCHITECTURE.md §1`): every line from the client,
  our data, the oracle, or permissive code. NF source is last resort; prefer
  deriving facts from client behavior even when slower. Never NF text, never
  paraphrase, never their discretionary architecture.
- **Openness Law** (`§1a`): everything documented and pushed to the public repo.
  No private stashes, no gating. A change not pushed is not done.
- The public commit history is both the provenance defense (timestamped
  clean-room evidence) and the standing rebuttal to flow-control culture.

## The frozen realm (the oracle) — do not disturb

The current NexusUnleashed realm keeps running and being played on Windows
(`realm-portable`) and on the Linux VPS (`NU-Linux`, private). It is the answer
key: every new component is correct when a player cannot distinguish it from the
frozen realm on the wire. It is not a rival to be retired — it is the reference.

## Next actions (for whoever picks this up)

1. Stand up the auth flow scaffold (SRP6a account handshake structure) against
   the account DB; keep the crypto constants UNPINNED and honest.
2. Build the content loader over our TSV/SQL so a world can be populated.
3. At the handshake step, capture one client session against the oracle to pin
   `spec/protocol/frame.md`, then flip the constants and delete the UNPINNED
   markers.
4. World entry → living world → systems → parity → NU-Linux deploy. In order,
   each pushed, each parity-tested.
