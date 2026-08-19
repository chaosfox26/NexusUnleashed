# THE NO-NF LAW

**Baked in by operator directive, 2026-08-19. Binary, non-negotiable, permanent.**

NexusForever — and everything derived from it — is **not a source for this
repository**. Not last resort, not "just to understand an algorithm," not for a
single fact. At all. This is why the project exists: an engine that owes NF
nothing.

## What is forbidden

Do **not** open, read, grep, decompile, copy, paraphrase, or take architecture,
naming, or "just the fact" from any NF-derived material to build anything here:

- `realm-source/recovered/**` — the decompiled old realm. It descends from NF and
  is AGPL. **This is the trap that looks clean because it says "NexusUnleashed"
  in the namespace — it is decompiled NF and is OFF LIMITS.**
- `realm-portable/servers/**` — the shipped assemblies (same lineage).
- The NF corpus: `Project Resources/_AllRepos`, `_ForkPool*`,
  `_Emulators-*`, anything named `NexusForever*`.

If a fact appears reachable only through one of those, that is **not permission**
— it means the fact must come from a clean source, or wait.

## The only clean sources (build from these, nothing else)

1. **The client** — Carbine's 16042 binary, its tables, its Lua, its behavior on
   the wire. The supreme authority. Facts here are free.
2. **Our own data & knowledge** — the restoration corpus, ledgers, laws, format
   cracks. Ours.
3. **The behavioral oracle** — observing the frozen realm's **wire** (packet
   captures). Its *behavior* is a fact and is clean; its *source code* is NF and
   is forbidden. This line is the whole discipline: **capture, never decompile.**
4. **Permissively-licensed code** — MIT/Apache/BSD, with attribution (e.g.
   Arctium, MIT).

## Enforcement

- Every `provenance/LEDGER.md` entry names a source in 1–4. An origin of
  "recovered/…", "NF", "the old realm's code", or any decompiled-NF path is a
  **provenance failure**.
- `provenance/nf-guard.py` mechanically scans the code for references into the
  NF-derived trees and fails the build on any hit. Run it in the gate.
- The guard catches textual references; **derivation is caught by discipline** —
  if you learned a formula or layout by reading NF, it is tainted even with no
  NF text in the file, and it must be re-sourced from the client or a capture.

## When clean and tainted give the same answer

They will, often — because the behavior is fixed by the client. That does **not**
make the NF reading acceptable. Build it *from the client* (extract from the
16042 binary) or *from a capture* (the client's behavior). Slower-but-clean beats
fast-but-tainted, every time, with no exceptions.
