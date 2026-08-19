# NexusUnleashed — Engine Architecture

_A clean-room WildStar server engine. Written 2026-08-19. This document is the
constitution of the new project: what it is, how it is built, and the laws that
keep it legally unassailable and technically faithful._

> **The one-sentence mission:** boot the server, log in, and find the game
> exactly as it was — the whole restored world, every system, every fight —
> running on an engine that is **ours, wholly, licensable however we choose,
> owing no one anything.**

---

## 0. Why this project exists

NexusUnleashed's restoration — 263,756 entities across 65 worlds, retail combat
kits, the whole game loading at boot — was built on an engine descended from
NexusForever, which is AGPL-3.0. The restoration itself (all data, all tools,
all knowledge) is ours and free. The **engine underneath it is not**, and cannot
be relicensed. Rather than live under a license wielded as leverage, we rebuild
the engine from scratch so the entire stack — engine and world alike — is ours.

This is legal, and it is the ordinary way emulators and clean-room clones are
built. Copyright protects **expression**, never ideas, methods, systems, or
facts (17 USC §102(b); the Altai abstraction-filtration-comparison test). A
WildStar server is, overwhelmingly, **obedience to Carbine's client** — message
shapes, sequences, table formats — which are facts dictated by an external
constraint and therefore unprotectable. What little is genuinely NF's creative
expression (their discretionary architecture and their literal code text) we do
not take.

---

## 0a. Acceptance criteria (operator, binding — the definition of done)

The project is not finished until ALL hold, measured not asserted:

1. **Login works.** A real WildStar 16042 client connects, authenticates, and
   reaches character select, then enters a world — against this engine.
2. **Deployable as the private server on NU-Linux.** It runs on the operator's
   Ubuntu VPS the way the current engine does: `install.sh`-class deployment,
   systemd services, the existing databases, one-command bring-up.
3. **As functional as the current engine.** Full behavioral parity with the
   frozen NexusUnleashed realm — the 263,756-entity world, the systems, the
   fights — indistinguishable to a player. The frozen realm is the oracle; the
   parity harness (§3) is the proof.

These supersede nothing in §0; they make it testable. "It compiles" is not done.
"It boots and a player cannot tell the difference, on the Linux box" is done.

## 1. The Provenance Discipline — the founding law

Every line in this repository is born from one of four **clean sources**, and
its origin is recorded. Nothing else is permitted in the tree.

1. **The client.** Carbine's 16042 binary, its tables, its Lua, its LuaDocData.
   The supreme authority; a fact from the client is free and outranks everything.
2. **Our data & knowledge.** The restoration corpus we authored: 263,756-entity
   world data, the bestiary, the ledgers, the session logs, the format cracks,
   the laws. All ours, all free.
3. **The behavioral oracle.** The frozen NexusUnleashed realm, still running.
   *Observing what a server does on the wire is not reading its code.* This is
   our parity test bench and our answer key.
4. **Permissively-licensed code.** MIT/Apache/BSD/public-domain sources may be
   incorporated with attribution. Identified in the corpus: **Arctium
   WildStar-Server (MIT)** — auth, handshake, and packet framing; the protocol
   bootstrap, legally reusable.

**The spec bridge — effectively disused by design (hardened 2026-08-19 against
the strict-clean-room challenge).** The strong form of our position is not "we
read NF carefully"; it is **"we almost never need to."** Our answer key is the
client and the frozen realm's observable behavior, not NF. So the standing rule
is: exhaust sources 1–4 — and specifically, prefer deriving a fact from the
client's *behavior* (packet capture against the oracle, table analysis) over
reading NF, even when reading NF would be faster. NF source is the source of
**last resort**, and every component's provenance-ledger entry must name a
source that is NOT NF wherever humanly possible. When NF is genuinely the only
door, reading it incurs no license obligation, but only a **fact, restated in
our own words**, crosses into a spec file — implementation then happens from the
spec with NF source closed, in our own architecture. **Provenance we can show
(this fact came from this packet / this table / this ledger) is the defense;
purity we can only claim is not.** A component whose behavior is fixed by the
client is not NF's expression no matter who else implemented it — that is the
Altai filtration, and it is most of the engine.

**Forbidden, absolutely:** NF literal code, comments, or snippets; translation
or paraphrase of NF code; reproduction of NF's discretionary architecture,
class decomposition, or naming where the problem did not force it.

**The parity oracle, not the parity source.** We match NF's *behavior* because
we both obey the same client — never because we copied how NF achieved it.

---

## 1a. The Openness Law (operator directive, 2026-08-19)

Everything implemented, from the foundation onward, is **documented and pushed
to the public repository** — every update, every system, every fix, in the open.
No private stashes, no "shared when it's clean," no DM-tier hoarding, no gating
of any kind. This is not a courtesy; it is the reason the project exists. The
public commit history doubles as the provenance defense (timestamped, immutable
evidence that every piece came in clean) and as the standing rebuttal to a
culture that controlled the flow. A change that is not pushed is not done.

## 1b. The Full-Load / Optimize-Underneath Law (operator directive, 2026-08-19)

**The whole game stays loaded — always — and every layer underneath it is
optimized relentlessly.** We never trade "the whole game resident" for speed;
instead we make the resident whole game *be* fast. A clean-room rewrite is the
one chance to pick the right data structure at every layer from line one, and we
take it: a spatial hash instead of nested grid walks, parallel world ticks by
default, zero-copy pipelined networking, struct-friendly hot paths, the GPU and
all cores assumed available (the Starlight protocol as an engine principle).

This is already visible: the clean engine holds **all 2,729 worlds resident in
~98 MB, ticking every one in 0.2 ms**, where the frozen realm needed **8.2 GB
and ~12.5 minutes** to sweep 1,767. Optimization here is measured, not claimed —
every load-bearing brick reports its number (worlds resident, entities/sec, tick
time, bytes) so a regression is visible immediately. The rule is simple: load in
full, and beat the old engine on every axis while doing it.

**Runtime portability clause (operator directive, 2026-08-19): the shipped
engine must load flawlessly on ANY hardware.** The server never *requires* the
GPU, a specific core count, or large RAM — it must run on a modest VPS, a single
core, no GPU. Parallelism is **opportunistic**, not mandatory: `Parallel.ForEach`
uses whatever cores exist and produces identical results sequentially on one
core; there is no GPU dependency anywhere in the runtime; "full load" scales down
(all worlds already fit in ~98 MB). Missing hardware degrades performance, never
correctness, and never crashes. This preserves the frozen realm's founding value
— unzip on a bare box and play. The **Starlight protocol (32 cores + the 5090)
is a dev-time and tooling principle** for our searches, sweeps, and batch
analysis on the operator's machine; it must never become a runtime requirement of
the engine we ship. Two machines, two rules: tools assume Starlight; the server
assumes nothing.

## 2. What carries over on day one (already clean)

Measured by `provenance-audit.py` against the frozen tree (151,021 engine lines):

| asset | status | into the new engine |
|---|---|---|
| **All world data** (263,756 entities, kits, floors, transport, ledgers) | ours | verbatim — it is TSV/SQL, engine-agnostic by design |
| **The entire tool stable** (NUSE, zone-forge, the exporters, all Python) | ours | verbatim — never touched NF code |
| **All knowledge** (session logs, laws, format docs, the Proofs) | ours | verbatim — it is the spec source |
| **A-AUTHORED engine code** (13,708 lines, 9.1%) | ours (your copyright) | lifted, surfaces retargeted to the new architecture |

The generators shrink the rest: network message models (~21.6K lines) are
protocol facts → **generated from spec**; GameTable models → NUSE `models.json`
*is* the spec → generated; enums → client facts → generated; database models →
our own schema → EF-scaffolded. The true hand-written creative core after all
levers is **~40–60K lines** of entity/map/spell/AI/session logic — spec-first,
parity-tested against the oracle.

---

## 3. Engine shape (subject to design, not to NF's design)

A .NET server, cross-platform by nature. Layered so provenance and parity are
enforceable at the seam, not by good intentions:

- **`Protocol`** — wire facts, generated. Opcodes, message layouts, framing,
  crypto handshake. Spec-driven; Arctium (MIT) seeds the bootstrap. Zero NF.
- **`GameData`** — the client's tables and static facts, read by our own
  readers (NUSE lineage). The `models.json` spec generates the typed accessors.
- **`World`** — entity, map, grid, movement, vision. The behavioral core. Every
  component carries an oracle parity test (stand beside the frozen realm; a
  player cannot tell which is which).
- **`Systems`** — spells, AI, combat, quests, events, and the authored systems
  (duels, rewards, hazards, sweep, teleporters…). The A-AUTHORED code lands
  here first; B/C systems are re-implemented from behavior specs.
- **`Content`** — the world data loader. Consumes our TSV/SQL unchanged.
- **`Server`** — the service hosts (world, auth, etc.), session lifecycle,
  persistence. Our schema, our config.

**The parity harness is a first-class subsystem, not an afterthought:** it
drives both engines from recorded/synthesized client sessions and diffs the
wire. Green means indistinguishable. That is the definition of done for §0.

---

## 4. Licensing (the operator's choice, made consciously)

The engine is ours to license freely. The trade-off is on the record: a
permissive license (MIT) lets anyone — including hoarders — take it closed;
a copyleft-of-our-own-choosing keeps it open for its users the way AGPL was
*supposed_ to be used before it became a cudgel. **Decision deferred to the
operator; recorded here when made.** The world data + tools + knowledge are
released as freely as possible regardless — that half of the promise stands
independent of the engine's license.

---

## 5. Build order (the roadmap lives in tasks; this is the spine)

0. **Foundation** — repo, this document, the provenance ledger, the parity
   harness skeleton, the generators' spec inputs.
1. **Protocol up to login** — Arctium-seeded handshake + generated messages
   until a client reaches character select against the new server.
2. **World entry** — a character loads into a world with our content data;
   entities appear. First oracle parity milestone.
3. **The living world** — movement, vision, spawns, AI, combat to parity.
4. **The systems** — A-AUTHORED lifts first, then B/C re-derivations, each to
   oracle parity.
5. **The whole game** — the full restoration runs on the new engine;
   indistinguishable from the frozen realm at the wire. §0 satisfied.

The frozen NexusUnleashed realm keeps running and being played throughout. It
is not a rival; it is the answer key.
