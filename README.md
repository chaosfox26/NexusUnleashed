# NexusUnleashed

A clean-room WildStar server engine, built from the client, our own restoration
data, and the observable behavior of a running reference realm — owing no
upstream license anything.

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
