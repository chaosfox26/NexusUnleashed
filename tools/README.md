# tools — clean-engine tooling index

Everything is client-derived (no NF, no captures). Newest first.

## Data / content resolvers (Python, committed)
| tool | purpose | added |
|---|---|---|
| `loadout.py` | Authoritative starter-gear + appearance resolver — from the client's own tables, gives the correct starter items and their rendered `displayId`s for any race/class/sex (drives the 0x0262 body + equipped visuals). No hand-picked ids. | 2026-08-22 |
| `client_tbl.py` | Correct, parallel WildStar `.tbl` reader/dumper — model-free (column names/types from the file), fixes the record-close/trailing-pad bug that stopped older readers on `Item2`. `--dumpall`, `--verify` (validated vs the engine dumps). | 2026-08-22 |

## Live client instrumentation (Frida + client-drive) — `live-probes/`
The probes and client-drive scripts used to RE and verify the retail client for world entry and the
standing-pose work. See **`live-probes/README.md`** for the per-tool table. Highlights:
`watch_live.py` (live monitor + SetStandState hook), `dump_entity.py` (dumps the client's parsed
0x0262 struct), `anim_tick.py` (is her animation ticking?), `spline_probe.py`, `health_scan.py`,
`getdesc.py` (opcode → wire descriptor); drivers `wslaunch/wsclick/wsvk/ws-shot.ps1`, `drive_login.sh`.
Added 2026-08-21/22. `%TEMP%\claude` holds the wider (uncurated) probe pile.

## Client-format RE — `client-re/`
`sts_re.py` + notes — STS/SRP login reverse-engineering. See `client-re/README.md`.

## Capture tooling (C#)
| dir | purpose |
|---|---|
| `NexusUnleashed.CaptureAnalyzer` | analyzes captured packet streams (opcode/codec validation). |
| `NexusUnleashed.CaptureProxy` | MITM proxy for capturing/replaying the client↔server wire during RE. |

> Build/RE notes for the engine itself live in `../cpp/docs/CODE-NOTES.md` (C++) and
> `../docs/CODE-NOTES-csharp.md` (C#). Source stays pure code (no build notes inline).
