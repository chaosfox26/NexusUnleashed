# live-probes — client instrumentation for clean-engine RE (2026-08-21/22)

Frida probes + client-drive scripts used to reverse-engineer and verify the retail 16042 client
against the clean engine, entirely from the client's own code (no NF, no captures). These were built
during the world-entry / standing-pose work. They log to `%TEMP%\claude\*.log` and target
`WildStar64.exe`.

**Prereqs:** `pip install frida frida-tools`; the client running; PowerShell for the `.ps1` drivers.
**Key addresses** (imagebase 0x140000000): session = `*(base+0xC65898)`, player unit = `*(session+120)`.
**LAW:** read-only on live pointers — writing a live spline-node pointer once CRASHED the client.

## Frida probes
| tool | what it measures |
|---|---|
| `watch_live.py` | live field monitor (position, +440 stand-state, +4896, velocity) + hooks SetStandState (sub_14045BF30) with a call-stack; reports only on change. The main "operator drives, I watch" monitor. |
| `dump_entity.py` | hooks the client's 0x0262 entity reader (sub_140096FA0) and dumps the parsed struct (propCnt, movCnt, movement type + position, factions, selectors) — proves what the client actually parsed from a spawn. |
| `anim_tick.py` | counts calls to the per-frame anim update (sub_1405B5070) and the play-anim fn (sub_140474400) for the player unit — tests whether her animation is ticking at all (the current standing-pose lead). |
| `spline_probe.py` | reads the movement subsystem node lists (unit+3936 etc.) to see if she is parked on a spline; also tests whether writing position sticks. |
| `health_scan.py` | scans the live unit for the health value — proved HP is at unit+444/+464 (=250/250), NOT +440 (that's stand-state). |
| `locostate.py` | dumps the locomotion/anim state fields (+128,+440,+444,+460,+464,+3408,+4896,+4932,+5160…). |
| `move_test.py` | logs unit+4576 position over time while W is held — proved movement is fully locked (position frozen). |
| `pose_transition_test.py` | calls the client's SetStandState Sit→Stand to test if pose transitions change the render (they don't). |
| `pose_hammer.py` | hammers unit+4896=0 to test if it drives the render (it does NOT — it's a velocity blend). |
| `call636.py` | calls the 0x636 SetPlayerUnit handler (sub_14057A630) live to test player-control activation (no effect). |
| `getdesc.py` | enumerates a message opcode's descriptor (size + read-fn) via the msg-manager vtable — how wire formats were recovered. |
| `uimon.py` | monitors Lua events + world/realm dispatch opcodes + the 0x25E handler — steady-state client event stream. |

## Client-drive scripts (PowerShell / bash)
| tool | what it does |
|---|---|
| `wslaunch.ps1` | launches WildStar64.exe with the `/auth` cmdline (CreateProcessW; must begin with /auth, not the exe token). |
| `wsclick.ps1 <x> <y>` | click at screen coords (Enter Game button ≈ 1276,1388 at 2560×1440). |
| `wsvk.ps1 <vk> <ms>` | hold a virtual-key for N ms with a proper scancode (so DirectInput gameplay sees it). W=0x57, P=0x50, Esc=0x1B. |
| `ws-shot.ps1 <path>` | screenshot ONLY the WildStar window to `<path>`. |
| `drive_login.sh` | full loop: wait → wslogin → Enter Game (1276,1388) → wait → screenshot. **Note:** it invokes `wslogin.ps1`, which is NOT committed (it carries the local test-account email/password) — recreate it locally: foreground the window, click email/pass fields, type the test creds, click Log In. |

> These are pragmatic scratch tools (hardcoded window coords for 2560×1440, `%TEMP%\claude` logs).
> Adjust coords/log paths for the environment. The full RE narrative they produced is in
> `Claude/Context/SESSION-2026-08-21-world-entry.md`.
