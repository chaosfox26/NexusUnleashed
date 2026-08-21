# CONTINUE HERE — NexusUnleashed clean engine: full continuation handoff

> **🟢 2026-08-20 — THE CLIENT REACHES THE CHARACTER CREATOR.** Both login walls fell
> (packet cipher = qword-CFB; realm dial address in the `0x03db` body). The real client now
> logs in, connects to the realm, is served its characters, and runs the **entire character
> creator**. **READ `SESSION-2026-08-20-character-creator.md` FIRST**, then the resume banner
> in `STATE.md`. NEXT = Phase 07: create-character (`0x5CD5`) → persist → the world server.
> The banner below is prior state.

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

A real WildStar **16042** client authenticates **end to end** against this
clean-room engine and reaches the realm channel — the crypto/login gate that stops
every emulator is fully cracked (SRP, encrypted channels, token handoff, realm
handshake). The client sits at "Retrieving Account Information", one step before
character-select. The remaining work to "standing in the world" is **message-body
reverse-engineering**: read each message's wire layout from the client's own
deserializer and regenerate it from our database. The immediate blocker is the
character-list (`0x0117`) wire format.

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

## 6. THE NEXT PROBLEM & THE SAFE PLAN

**Blocker:** the client waits at "Retrieving Account Information" (an account-info
message, family `0x036`/`0x0AD`/`0x33D`, is expected before character-select), and
then needs a valid **`0x0117` character list**. Both bodies must be generated from
the client-derived wire layout + our characterdb.

**The hard part is the `0x0117` wire format.** The parse is done by a generic/
schema-driven deserializer that is elusive statically (the `{0x38,0x50,0x5c}`
struct-write signature matches 133 functions; a naive u32-opcode registry search
found nothing). SAFE approaches for next session:
1. **Static:** find the pump via the `G` vtable (`OnMessage` ptr at `.data
   0x140C66D58`) → read the per-opcode read path / locate the message factory and the
   `0x117` Read. Or find Carbine's bit-reader (hot, called by every deserialize) and
   read the `0x117` field/width sequence.
2. **Sandboxed dynamic (safe):** once the deserializer is located, Frida-`NativeFunction`
   it in-process with a controlled buffer to watch the parse — NO network, NO
   live-client-state risk. Or hook it and observe a *genuinely valid* parse only.

Then build the **generic, account-keyed** `0x0117` generator (reads whichever account
authenticated → serializes its characters). Then: character-select (client→server) →
world entry (`0x0988` world payload, `0x0981` init, self block, `0x0262` entity
stream — each client-derived, filled from our world data).

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
