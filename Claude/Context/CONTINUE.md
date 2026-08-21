# CONTINUE HERE — NexusUnleashed clean engine: full continuation handoff

> **🌟 2026-08-21 — THE NORTH STAR IS REACHED: THE CHARACTER STANDS IN THE 3D WORLD.**
> The real 16042 client now goes login → realm → char-select → Enter Game → **stands in the
> arkship Medbay as a full Aurin-female body**, fully SERVER-NATIVE (zero Frida in the path),
> zero NF, zero captures. **READ `SESSION-2026-08-21-world-entry.md` — the "FINAL STATE" section
> at the bottom is the authority** (the complete recipe + the load-mask mechanism).
> THE RECIPE (world_handshake.cpp, realm connection): 0x00AD world-enter, then on movement:
> 0x00AD(2nd ChangeWorld) + **0x00F1 (16 ZERO bytes → session+25632=1)** + 0x0262 player entity
> (with race/sex + item-visuals so the BODY renders) → 0x019B set-player → 0x0061 PlayerEnteredWorld
> + 0x0845 timer keepalive. THE MECHANISM: the client's world-load mask at `session+31560` must
> reach **0x7F** (bits: 0-3 map=auto, 0x10 needs 0xF1, 0x20|0x40 need 0x61); its per-frame update
> drops the loading screen only at 0x7F. NEXT = Phase 08 polish: standing pose (she renders LYING
> DOWN — a stand-state/unit-alive flag), exact floor Y, per-character appearance from the DB, then
> the living world (movement/entities/combat/quests). §6 below is rewritten for this. Older banners
> are prior state; §4's char-list/login detail still holds and is upstream of all of the above.

> **🟢 THE ENGINE IS NOW C++ (2026-08-19).** The C++ port reached **parity with the C#**
> and is the project's primary engine: a real 16042 client authenticates end-to-end
> against it, enters the realm channel, and is served its char list from the DB — proven
> live. **C# is now an afterthought** (historical reference/oracle only; do not add
> features to it). **📌 READ `../../build-notes.md` FIRST — it is the go-to record of
> what has been built.** Then `CPP-PORT-PLAN.md` for scope/vision. The protocol RE below
> still stands (language-neutral specs; Frida/Python tooling is language-independent).
> Everything in §2 THE RULES still applies (esp. NO NF). The one remaining step to the
> world is the realm-enter → char-select transition (build-notes.md §5).

_Self-contained. Read this first, then `STATE.md` (resume banner) and
`SESSION-2026-08-19-login-and-tools.md` (evidence). Sensitive specifics a fresh
session needs (test-account handling, character names, exact dev machine, login
driver paths) are in the gitignored `Claude/Context/local-notes.md` — never public._

---

## 0. One-paragraph state

**The north star is reached.** A real WildStar **16042** client authenticates end to end,
creates a character, and **stands in the 3D world** (arkship Medbay) against this clean-room
engine — rendered as a full Aurin-female body, entirely server-native (zero Frida), zero NF,
zero captures. Every gate that stops emulators is cracked: SRP login, the encryption channel,
the realm handshake, character list + creation, AND world entry (the load-completeness mask at
`session+31560` fully RE'd → driven to 0x7F by our own messages). What remains is **content on a
proven foundation** (Phase 08): the standing pose (she renders lying down), exact floor Y,
per-character appearance from the DB, then movement/entities/combat/quests. Nothing left is
"can we do it" — the hard, uncertain parts are all behind us.

---

## 1. The mission (the north star)

**A real client, logged into THIS engine, standing in the world — on our engine,
not NexusForever's.** Everything is subordinate to that. The project exists to
escape AGPL-3.0: a from-scratch, **MIT**, clean-room WildStar server that owes NF
nothing and is free for the whole community with zero restrictions.

---

## 2. THE RULES (non-negotiable — these override defaults)

### Provenance
- **NO NexusForever. Not servers, not source, not protocol.** (Operator, hardened:
  "We do NOT use NF servers", "no NF protocol".) Opcodes and formats come from
  **Carbine's client** (its dispatch + its deserializers) and **our own DB/data**.
  Uncopyrightable facts (protocol/opcodes/formats) are defined by the client, free
  to implement. **Do NOT use NF-server captures as a protocol/format source** — the
  server→client bytes in `realm-source/captures/` were produced by the NF server and
  are reference-poison. (Client→server bytes in a capture are the client's own and
  are clean, but prefer deriving from the client binary.)
- **Order of authority:** the client (its tables, Lua, binary) → our own tree → the
  corpus → the open web. Never paste a value from search without confirming it.
- Every component carries a provenance note; `provenance/nf-guard.py` scans for NF
  leakage. `provenance/privacy-guard.py` must pass before any push.

### Privacy (these files — STATE, SESSION, README, specs — are PUBLIC)
- **Never commit:** character names, account emails/logins, real names, IP
  addresses, or the operator's personal info. Refer to characters/accounts by
  neutral ids ("the target character = characterdb id N", "the test account").
- The privacy guard's term list is `provenance/.private-terms` (gitignored). Add
  any new personal term there. **`privacy-guard.py` must report CLEAN before push.**
- The already-pushed history is clean; keep it that way (scrub the working copy
  before committing, not after pushing).

### Hardware-first (the Starlight protocol)
- **The dev machine takes priority and is the design target from line one.** Tools
  are built for the hardware — multi-core CPU, CUDA GPU, ample RAM — never
  single-core hand-loops. Batch/scan/brute-force work goes on all cores or the GPU
  bridge by design. Hand-work is allowed only when there is genuinely no way to put
  the machine on the job. See `HARDWARE REQUIREMENTS` below.
- **Never destabilize the live client.** (This session's lesson: sending malformed
  messages to the live client to trigger its parser CRASHED it. Do not do this.) RE
  is **static-first**; dynamic instrumentation must only *observe genuinely valid
  parses* or call functions in an **isolated sandbox** (Frida `NativeFunction` with a
  controlled buffer, no network) — never gamble the client's stability.

### Reproducible, multi-account, community
- **Reproducible for anyone:** no magic constants, no per-machine hacks. Engine,
  specs, and RE tooling are public and buildable by anyone.
- **Multi-account and generic by design:** every per-account thing (the character
  list first) is generated from the DB **keyed by whichever account authenticated**.
  The operator's main account must work by the identical code path as the test
  account. Never hardcode a character or account.
- **MIT, no strings.** Full freedom, no requirements. That is what Nexus Unleashed
  offers.

### Working with the operator
- **When the operator says ANYTHING — question, correction, comment — STOP and
  answer FULLY first, before any further tool calls, even mid-task.** A correction
  replaces the current plan; restate it as the new plan before touching anything.
- **Continuous mode:** proceed through discussed work without asking per step, but
  verification still runs and the hard rules above are never overridable. "We're not
  done until I'm in the world" = keep driving toward the north star.
- **Git:** commit as `chaosfox26-ai` with an EMPTY email (`-c user.email=""`).
  **Never push without being asked.** (The operator authorized pushing the
  README/roadmap; other pushes need a fresh OK.) Privacy guard must pass first.

---

## 3. HARDWARE REQUIREMENTS

**Reference dev machine ("Starlight")** — the hardware-first design target. Exact
model strings are in `local-notes.md`; the general requirements:

| need | requirement |
|---|---|
| CPU | high core/thread count (reference: 32 threads). Batch RE scans use all cores. |
| GPU | CUDA-capable, **Blackwell / RTX 50-series** class. torch must be the **cu128** wheels for Blackwell (see the GPU bridge / `verdict-engine.py` reference in the sibling project). |
| RAM | ample (reference: ~64 GB) — materialize datasets in memory, don't lazily re-inflate per subscript. |
| OS | Windows 11 for dev + the client oracle; the engine also builds a self-contained linux-x64 ELF. |

**Software toolchain:**
- **.NET 10 SDK** (the engine is C#, `net10.0`).
- **MariaDB** — the engine reads accounts/characters. Bundled DB runs on **port 3307**.
- **Python 3.13** with `pefile`, `capstone`, `frida` (17.x) for RE. Install:
  `python -m pip install pefile capstone frida frida-tools`.
- **The WildStar 16042 client** as the behavioral/format oracle:
  `realm-portable/clients/Wildstar/Client64/WildStar64.exe` (imagebase `0x140000000`),
  `StsConnLib64.MT.dll` (STS/SRP). The full 16042 CDN mirror is available locally
  (see the sibling realm-portable project's notes).

---

## 4. TECHNICAL STATE — what is cracked (all client-derived)

### Ports (this engine)
- **STS login: 6600** · **realm/auth: 23115** · **world: 24000** · **MariaDB: 3307**.

### The login chain (WORKING end to end)
1. **STS** (text/HTTP-shaped on 6600): `/Sts/Connect` → `/Auth/LoginStart` →
   `/Auth/KeyData` → `/Auth/LoginFinish` → `/GameAccount/ListMyAccounts` →
   `/Auth/RequestGameToken`.
   - **SRP is WildStar's game SRP, LITTLE-ENDIAN** (N read LE, ReverseUInt32 word-order
     hashing, LE bignums, interleaved session key). `StsSrp.cs` mirrors
     `SrpReferenceClient.cs`. Post-SRP the STS channel is **ARC4(sessionKey)**.
   - Reply envelope `<Reply>`; status line `STS/1.0 200  OK` (**two** spaces);
     `s:<seq>R` header; `<KeyData>` = base64 length-delimited blob.
   - **`LoginFinish` `AuthType` = the string `Password`** (not `"1"`).
   - **`ListMyAccounts`**: records are DIRECT children of `<Reply>` — **no
     `<Items>`/type="array"** wrapper (those strings don't exist in StsConnLib; its
     parser is `[reply+0x60]`=first record, `[item+0xc0]`=field). Include the FULL
     GameAccount field set the client reads (GameAccountId, AccountId, LoginName,
     UserId, UserName, Email, Alias, AccountAlias, GameCode, AppId, UserCenter, State,
     Status, Roles) — a missing string field makes WildStar64.exe `strlen(null)` →
     AccessViolation at RVA `0xB3885`.
2. **Realm channel (23115):** opens with a **clear `0x0003` hello**, then the
   **auth-key encrypted container** (`0x0244` in / `0x03DC` out). `WorldHandshake`
   bootstraps clear-then-container off `Crypt==null`. The client's realm-enter is
   inner op **`0x0592`** (`[build 16042][8B][login-name UTF-16][fields][client system
   survey]`). **The live client uses `0x0592`, not the `0x058F` an older capture read.**
3. **Keying:** the channel STAYS on the auth key (`WorldChannelSeed` =
   `0xD283F5B34A8DC685`) through character-select — proven because the client's
   post-enter `0x0000` decodes with it. No world re-key until world entry.

### The login-message dispatch (client-derived, WildStar64.exe)
- Realm-message handler `fn 0x140020EA0` (`G::OnMessage(this, arg, opcode r8d, msg
  r9)`); opcode switch head `0x140020EF1`. **CFG-trace the compare tree per-branch**
  (linear cumulative-sub is WRONG). Opcode → case map (validated `0x117→0x21167`):

  | opcode | role (Lua events fired) |
  |---|---|
  | 0x036 | MaxCharacterLevelAchieved, CharacterDisabled, CharacterSelectFail |
  | 0x0AD / 0x33D | SubscriptionExpired, GameTimeHoursRemaining, RealmTransferFlags |
  | 0x0E7 | CharacterDisabled, CharacterSelectFail |
  | **0x117** | **CHARACTER LIST** (handler `0x140021540`, char struct stride 0x330) |
  | 0x36A / 0x3E1 | QueueFinished, TransferDestinationRealmList, RealmBroadcast |
  | 0x116, 0x14B, 0x594, 0x715, 0x717, 0x761, 0x765, 0x862 | further realm msgs |

- **Character-list opcode `0x0117`** RE chain: Lua `HasReceivedCharacterList` → reads
  global `0x140C66DA8`+0x168 → the sole writer is handler `0x140021540` (sets
  `[this+0x168]=1`, parses chars at stride 0x330, fires the `CharacterList` Lua event)
  → its only `.text` xref is dispatch case `0x140021167` → the compare tree gives
  `0x117`.
- The dispatch is a **vtable method**; its pointer is at `.data 0x140C66D58`. The
  message is a **deserialized struct** when the handler runs (fixed offsets: `+0x8`
  char vector, `+0x18`/`+0x20` a string(len/ptr), `+0x28`/`+0x30`, `+0x38`, `+0x40`/
  `+0x48`, `+0x50`, `+0x5c`), so the wire parse happens in the pump BEFORE dispatch.

---

## 5. HOW TO RUN

Repo: `Desktop/realm-portable/NexusUnleashed-Engine` (public: github.com/chaosfox26/NexusUnleashed,
branch `master`).

```bash
# Build (ALWAYS kill the running engine first — it locks the exe):
powershell -NoProfile -Command "Get-Process NexusUnleashed.Realm -EA SilentlyContinue | Stop-Process -Force"
dotnet build src/NexusUnleashed.Realm/NexusUnleashed.Realm.csproj -c Release --nologo
# assert "0 Error(s)" — a copy-lock error means the engine is still running.

# Run (from the build output dir):
cd src/NexusUnleashed.Realm/bin/Release/net10.0 && ./NexusUnleashed.Realm.exe > engine.log 2>&1 &
# Expect 3 "listening" lines (STS 6600 / realm 23115 / world 24000). MariaDB must be up on 3307.

# Login test: drive the real client yourself (screenshot + PowerShell SendInput).
#   The login-driver script + coordinates + which client window are in local-notes.md.
#   The engine logs the full flow; STS request bodies -> sts-capture.log beside the exe.

# RE tools (Project Resources/Tools-Working/Tools):
#   bin-re.py         -> disasm/strxref/xref on the client binaries
#   re/ws-trace.py    -> Frida tracer: attaches to WildStar64.exe, hooks the dispatch
#                        (RVA 0x20EA0) + char-list handler (RVA 0x21540). Frida 17 API:
#                        Process.getModuleByName(name).base  (getBaseAddress removed).
```

Provenance/privacy gates before any push:
```bash
python provenance/privacy-guard.py    # must say CLEAN
python provenance/nf-guard.py         # no NF leakage
```

---

## 6. THE NEXT PROBLEM — PHASE 08 POLISH (content on a proven foundation)

World entry is SOLVED (see the banner + the session log's "FINAL STATE"). The remaining
items are well-scoped and none are "can we do it":

1. **STANDING POSE (top priority).** She renders LYING DOWN. Her character data is correct
   (the portrait renders fine), so this is a **stand-state / unit-alive flag** on the spawn
   entity (`0x0262`) that isn't set — likely a StandState field or the unit's health/alive
   state defaulting to a collapsed/dead pose. NEXT: RE the client's StandState / the unit
   component's alive+stance fields; find which entity field drives the standing idle.
2. **EXACT FLOOR Y.** DB saves her at (1437.82, 85.53, -106.82); that clips into the medbay
   floor. `TWY` in `world_handshake.cpp` is bumped to 86.10 (still low; real floor ~87-88).
   **The client IGNORES memory writes to player+4580**, so calibrate by server rebuild+relog,
   not a Frida pin. (Or read the medbay floor from the client's collision at that XZ.)
3. **PER-CHARACTER APPEARANCE FROM THE DB.** `BuildPlayerEntity` currently HARDCODES Peryanna's
   (char id 32) race/sex + 7 item-visual slots. Wire it from characterdb: `character` (race/sex/
   class) + `character_appearance` (slot→displayId, the `a3+176` array), keyed by the character
   being entered. Full face customisation = `character_customisation` (label→value) into the
   Player-block arrays (`a3+48` u32s / `a3+76` u64s / `a3+88` u32s).
4. **THE LIVING WORLD (Phase 3 proper):** movement steady-state, entity streaming, spells/combat,
   quests, loot, vendors, chat/groups — each client-derived, filled from our world data.

**Key mechanism refs** (all in `SESSION-2026-08-21-world-entry.md`): the load-mask
(`session+31560` → 0x7F); the session per-frame update `sub_1403E85D0`; the mask-bit setter
`sub_1403E8000`; PlayerEnteredWorld `sub_1403C74D0` (0x61); world-entry init `sub_1403B67E0`
(0x00F1, all-zero body); item-visual reader `sub_1400AB890` (`[7b slot][15b displayId][14b][32b]`).

---

## 7. FILE MAP

- `src/NexusUnleashed.Sts/` — STS server, `AuthFlow.cs` (the login chain),
  `StsServer.cs` (ARC4 channel + reply logging), `StsSrp.cs` (game SRP).
- `src/NexusUnleashed.Realm/` — `Program.cs` (host + ports), `WorldHandshake.cs`
  (realm channel + `0x0592` handler where the char-list send goes), `AuthHandshake.cs`
  (older clear-channel notes; realm now uses WorldHandshake).
- `src/NexusUnleashed.Network/` — `GameServer.cs`/`GameSession.cs` (container framing,
  `Crypt`), `PacketCrypt.cs` (two-phase cipher), `WorldPacket.cs`.
- `src/NexusUnleashed.Database/DbAccountStore.cs` — reads authdb; add characterdb reads
  for the `0x0117` generator here.
- `spec/protocol/` — `sts.md`, `containers.md`, `world-entry.md`, `observed-opcodes.md`
  (note: `0x0117` was mislabeled "player self block" there — it is the character list).
- `provenance/` — `privacy-guard.py`, `nf-guard.py`, `.private-terms` (gitignored).
- `Claude/Context/` — `STATE.md`, this file, the session log, and (gitignored)
  `local-notes.md`.
- RE tooling: `Project Resources/Tools-Working/Tools/re/ws-trace.py`, `bin-re.py`.
