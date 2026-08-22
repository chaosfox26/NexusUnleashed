# VM Resume Prompt — clean-engine standing-pose deep dive

Paste the **RESUME PROMPT** block below into Claude on the VM once the prerequisites are installed.

---

## Prerequisites the VM needs

Install/verify these before resuming (fresh environment):

- **Windows 11** (the retail WildStar client is Windows-only).
- **Visual Studio 2026** (installs under `...\Microsoft Visual Studio\18\`) with the
  **"Desktop development with C++"** workload — this provides MSVC, the Windows SDK, and the bundled
  CMake. The build invokes that CMake directly at:
  `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe`
- **CMake** (bundled with the VS C++ workload above, or standalone on PATH).
- **vcpkg** (the C++ dependency manager the engine builds against) — bootstrapped, deps installed,
  referenced by the CMake toolchain. It lives OUTSIDE the repo (e.g. `%USERPROFILE%\vcpkg`).
- **Git**.
- **Python 3.x** on PATH, with:
  - `pip install frida frida-tools` — required for all the live instrumentation probe tools.
  - (the wider tool stack also uses numpy/torch for the GPU bridge, but that is NOT needed for the
    pose work.)
- **PowerShell** (Windows PowerShell or pwsh) — the client-drive scripts (`ws*.ps1`) are PowerShell.
- **The WildStar 16042 retail client** installed and runnable (the drive tools launch
  `WildStar64.exe`) — under `realm-portable\clients\Wildstar`.
- **The repo + realm present**: `realm-portable\NexusUnleashed-Engine` (the clean C++ engine) and the
  realm's `characterdb` with the test character. The engine's `cpp\build` should exist (or run the
  CMake configure step once).
- The Frida probe tools + client-drive scripts live in the session scratch dir
  (`%TEMP%\claude\`). If that dir is empty on the VM, they can be recreated from the recipes in the
  session log — but they are small; keeping a copy is easiest.

---

## RESUME PROMPT

Resume the WildStar clean-engine work — standing-pose deep dive.

Read these first, in order, before doing anything:
1. `Desktop\realm-portable\NexusUnleashed-Engine\Claude\Context\CONTINUE.md` (the resume anchor)
2. `...\Claude\Context\SESSION-2026-08-21-world-entry.md` → the section **"2026-08-22 STANDING POSE
   DEEP DIVE"** (full measured detail; last commit `72a593c`)

**Where we are:** The clean C++ engine (`nexus_realm`) drives the retail WildStar client into the
arkship (world 1537). The character is bound, correct appearance, full health, camera works — but her
**body is frozen in a lying idle and won't move**. Key measured finding: even an *unbound copy* of her
body lies down, and emotes flip her stand-state flag without changing her body — so it's **not** a
control/binding problem, her **model's animation isn't advancing**. All the wrong old leads
(unit+4896, +440-as-HP, spline, HP/death, intro) are disproven by live measurement — don't re-chase.

**Do this next:** run `%TEMP%\claude\anim_tick.py` against the running client to confirm whether the
per-frame animation update (`sub_1405B5070`) even ticks for her unit. If it doesn't → her animation
isn't ticking (root cause); then find why a world-spawned entity's animation set doesn't link/activate,
given char-select force-loads the *same* model and it stands. Probe tools are in `%TEMP%\claude\`.
**Read-only** on live pointers — writing a live spline node crashed the client once.

**Standing rules (still in force):** Continuous mode — don't stop to ask, keep working until it's
fixed. Derive ONLY from the client + our data (NO NF, no corpus). This is C++, not C#. Straight code
only (notes go in cpp/docs). Full-suite privacy sweep (`python provenance/privacy-guard.py`, EXIT=0)
before every commit; never push unless asked. You have the operator's grant to **drive the client
autonomously** (launch/login/enter) so it won't touch their keyboard — tools: `wslaunch.ps1`,
`wslogin.ps1`, `wsclick.ps1 1276 1388` (Enter Game), `ws-shot.ps1`, or `drive_login.sh`.
Build: kill `nexus_realm.exe`, then
`"C:/Program Files/Microsoft Visual Studio/18/Community/Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe" --build cpp/build --config Release --target nexus_realm`.

**Live state:** realm should be up; relaunch the client if needed. Test character: Peryanna,
characterdb id 32, class 7 Aurin F, world 1537.
