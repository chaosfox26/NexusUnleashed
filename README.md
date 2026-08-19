# NexusUnleashed

A clean-room WildStar server engine, built from the client, our own restoration
data, and the observable behavior of a running reference realm — owing no
upstream license anything.

## Take it. It's yours too.

**This project is [MIT-licensed](LICENSE), and we mean it in the most open way a
license can be read.** Use it, fork it, don't fork it, rip out one file or the
whole engine, build a commercial thing on it, build a hobby realm on it — you
need no one's permission and you owe no one an explanation. There is no
"contribute back" obligation, no gatekeeping, no private branch you have to earn
access to. Every commit is public from the first line.

Credit is appreciated, never required. If NexusUnleashed helps your project,
that's the entire point of putting it here. Nobody should have to negotiate for
access to a WildStar server — and with this repo, nobody does.

- `ARCHITECTURE.md` — the constitution: mission, the Provenance Discipline, the
  build order.
- `provenance/` — the ledger proving every component's clean origin.
- `spec/` — behavior specifications (the only thing that crosses from analysis
  to implementation).
- `parity/` — the harness that proves this engine is indistinguishable from the
  frozen realm on the wire.
- `Content/` — the world data (263,756 entities, kits, floors, transport),
  loaded unchanged.

The restoration data, the tools, and the knowledge are ours and free. This
engine makes the whole stack ours.
