# OPTIMIZATION — the whole point of the engine, on the record

> This document is a **permanent statement of intent.** It is not a to-do list and not a set of
> benchmarks; it is the standard the NexusUnleashed engine is built to. Every design decision,
> every data structure, every hot loop answers to what is written here. If a change makes the
> engine heavier without a measured reason, it is wrong — no matter how convenient.

## 1. The mandate

NexusUnleashed exists to escape the AGPL. But escaping the license was never the whole ambition —
it was the door. **The ambition is to build the WildStar server that every prior one should have
been: brutally lean, brutally fast, and scalable to the entire game running at once.**

The engines that came before were heavy. Managed runtimes with garbage-collector pauses. Heavy ORMs
on the database path. Per-entity object churn. Reflection-driven systems. They worked, but they were
clunky, and they left performance on the table that this project intends to reclaim in full.

**We are going to show them up — in the code and in the numbers.** Not by reading their code (see
§8), but by building ours right from the first line.

## 2. The North Star of performance

The definition of "done" for the engine is *a real client standing in the world.* The definition of
**done well** is bigger:

> **The entire game — every world, all of it — resident and ticking in parallel on a single box,
> under a memory budget you can dial in with a slider, spread across every CPU core.**

That is the target that shapes everything: not one zone, not one shard — **~2,760 worlds live at the
same time**, simulating in parallel, without the machine breaking a sweat. An engine that can do that
can do anything smaller trivially. So we build for the hardest case and let the easy cases fall out.

But that ceiling is a proof of *headroom*, not the everyday behavior. Day to day, the engine is
disciplined and lean: worlds load **on demand**, stay **compressed**, and only the ones actually in
use cost anything — with a warm cache so startup and transitions feel instant (see §7).

## 3. Non-negotiable principles

1. **Measure, never guess.** Every performance claim is backed by a number from *our own* profiler.
   "It feels fast" is not a metric. Budgets are set, and holding them is a pass/fail gate.
2. **Data-oriented from line one.** Hot data is laid out for the cache, not for the class diagram.
   Structures of arrays over arrays of structures where it counts; tight, contiguous, predictable.
3. **Memory compression is a first-class tool.** To keep every world resident, per-world memory must
   be *small*. We compress world/entity data in memory, pack fields, and refuse bloat. Footprint is
   a design constraint, not an afterthought.
4. **Zero needless allocation in the hot path.** No per-entity heap churn per tick. Pools, arenas,
   and reuse. Allocation is a cost you pay once, at load, not every frame.
5. **Multicore-first.** The load spreads across **all** cores. This is not theoretical — spreading
   the simulation across many cores was proven, on a Linux realm, to scale genuinely well. The
   engine's worker pool fans out across the cores the operator grants it; nothing is single-threaded
   that could be parallel.
6. **Cache-dense, branch-light hot loops.** The inner loops that run per-entity, per-tick are the
   ones that matter. Everything else can be readable; these must be fast.
7. **Native, no managed runtime in the engine.** C++, RAII, no GC, no hidden allocations, no
   reflection at runtime. The launcher and tools may be richer; the *engine* stays lean metal.
8. **Scalable to Linux, at scale.** The end state is this engine on a Linux server running the whole
   game. Everything is built so that "all worlds at once" is an intense-but-survivable load, not an
   impossible one.

## 4. Budgets, and how we prove them

Optimization that isn't measured is a wish. So we make it measurable and visible:

- **The launcher is the measurement tool.** `nusl.exe` puts a **hard memory cap** on the server via
  a Windows Job Object and a **CPU-core control** that sets both the affinity mask and the worker
  thread count. That means the target — "all worlds resident under N GB across M cores" — is
  something we can *dial in and watch*, live, with the built-in RAM/CPU meters. Memory compression
  and tight layouts become pass/fail against a real number on the slider, not a hope.
- **Per-tick and per-world budgets.** As the simulation grows, tick time and per-world resident
  memory get budgets. Blowing a budget is a regression, treated like a broken build.
- **Profiling is routine, not a fire drill.** We profile *our* engine to find where *our* time and
  memory go, and we optimize the real hotspots — never guessing, never optimizing the wrong thing.

## 5. Parallelism

The networking layer already runs an async `io_context` across a pool of worker threads, so
connections and packet work spread across cores by default. The simulation follows the same law:
work is partitioned so cores stay busy and independent. The core count is operator-controlled
(the launcher's slider → affinity mask + `NUSL_THREADS`), so the same engine scales from a modest
box to a many-core server without a rebuild.

## 6. Memory

Running every world at once is fundamentally a memory problem before it is a CPU problem. So memory
is treated with the same seriousness as speed: compressed in-memory representations, packed fields,
shared/immutable data loaded once, and a hard ceiling we can enforce and observe. The goal is a
per-world footprint small enough that the sum of all worlds fits comfortably under a sane cap.

## 7. Maps: compressed, cached, and demand-loaded

Holding every world at once is the *ceiling*. The everyday default is to be **lean**, and maps are
where that shows most. A world nobody is standing in should cost almost nothing.

- **Compressed on disk and in memory.** Map and world data is stored compressed and kept compressed
  in RAM, decompressed only the moment it's actually needed. Idle maps sit small; they do not pay
  full price for existing.
- **Demand-driven loading.** The engine loads the world in active use — **not the whole game.** If a
  player is in Everstar Grove, Algoroc is not loaded; there is no reason to pay for a world nobody is
  in. Worlds spin up when someone needs them and spin down when they empty out.
- **A warm cache for near-instant startup.** A small, smart cache of the maps most likely to be
  needed next — the starting zones, the world a player is entering, the zones adjacent to where they
  already are — is kept pre-loaded and ready. Startup is near-instant, and crossing a zone boundary
  costs the player *nothing*, because the next map was already warm in the cache.
- **The two live together, deliberately.** "All worlds resident at once" is the stress ceiling that
  proves the engine's headroom. "Load only what's in use — compressed, cached, on demand" is the
  disciplined default that keeps memory proportional to real demand and startup instant. The engine
  is built for both: it *can* hold everything, and it is disciplined enough not to when it doesn't
  have to.

## 8. The client side of the vision

Performance doesn't stop at the server. The broader vision includes modern client-side rendering
paths — upscaling (FSR 3/4, DLSS 3/4) and a modern graphics API (DX12) — so the whole experience,
server to screen, is as fast and modern as the engine underneath it. (Tracked separately; noted here
so the ambition is recorded in full.)

## 9. What we will NOT do

- **We will not read NexusForever's source to "learn how it's slow."** The engine's entire value is
  that it owes NF nothing — clean-room, from the client and our own work. Reading their AGPL code,
  even to critique its performance, would weaken that. We beat them from first principles and our own
  measurements, not by studying them. Their code is reference poison; it stays closed.
- **No premature abstraction** that costs cycles or cache for tidiness. Clarity yes, indirection tax
  no.
- **No heavy ORM, no reflection, no managed runtime** on the hot path.
- **No "we'll optimize it later."** Later is a design decision made now: the shape has to be fast, or
  it doesn't ship in the hot path.

---

*Living document. The principles are fixed; the specific budgets and numbers will be filled in and
tightened as the simulation grows. What does not change is the standard: this engine is built to be
the leanest, fastest WildStar server there has ever been — and to run the entire game at once.*
