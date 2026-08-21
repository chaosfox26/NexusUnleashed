# Session 2026-08-19 — laws, the tool suite, NU-deconstruct, and the login RE

Read this first on resume, then STATE.md. This session did a LOT; the live thread
is the **STS login RE** (§5). Everything is pushed unless noted.

## 1. Two laws BAKED IN + enforced (both guards GREEN)

- **No-NF law** — `ARCHITECTURE.md §1.0`, `provenance/NO-NF.md`, enforced by
  `provenance/nf-guard.py` (fails build on any reference into NF-derived trees).
  **THE TRAP: `realm-source/recovered/**` is decompiled NF (AGPL) despite the
  "NexusUnleashed" namespace — OFF LIMITS.** Only sources 1–4 (client / our data /
  oracle WIRE not code / permissive). Operator reaffirmed HARD, twice: "I'm not
  touching NF stuff again." Pure client RE only.
- **Privacy law** — `provenance/PRIVACY.md`, `provenance/privacy-guard.py` (scans
  tracked files for emails/private-IPs + terms in the gitignored
  `provenance/.private-terms`). Nothing personal (account name, character name,
  email, IP) may reach any public repo. Run both guards before every push.

## 2. Cipher — SOLVED then CORRECTED (two-phase keying)

Stateless-fixed-key, TWO keys per connection:
- **auth key** = `PacketCrypt.AuthChannelKey` = `0xD283F5B34A8DC685` (runtime-observed, clean). Used for the hello.
- **world key** = `GetKeyFromTicket(sessionKey)` — **QUARANTINED** (its formula was read from recovered/NF; `provenance/QUARANTINE-NF.md`). `RekeyForWorld(ulong)` now takes a keyInteger directly. World key recovered from the capture by cryptanalysis (`0x4888DCE5CA507060`) decrypts the whole world stream (proven). Must re-source `GetKeyFromTicket` from the CLIENT before world entry ships.
- Container framing `0x03DC`/`0x0244` = `[u32 innerLen][encrypted [u16 op][body]]`, proven byte-for-byte. Tests 28/28. See `spec/protocol/cipher-state.md`, `containers.md`.

## 3. The hardware-first RE tool suite (Starlight — the NEW tool template)

Operator directive, HARD: **every tool uses the GPU (5090) + 32 threads + RAM
core, by design, from line one. Every teardown/analysis gets a proper TOOL, not a
one-off snippet** (also saves my usage limit). Tools live in
`Project Resources/Tools-Working/Tools/`:
- `wildstar-deconstruct.py <exe> <outdir>` — full PE teardown (strings ASCII+UTF-16, disasm, functions, callgraph, string-xrefs, RTTI, Win32 api-surface). **capstone needs `md.skipdata=True`** or it halts at the first data byte (got 197K vs 2.64M insns).
- `bin-re.py <dll> <cmd>` — RE query toolkit. Commands: `strings [sub]`, `xrefs 0xVA`, `strxref <sub>`, `disasm 0xa 0xb`, `funcat 0xVA`, `callers 0xVA`, `ptrs 0xVA`, `vtables [sub]`, `vtreg [sub]`, `readq 0xVA n`, `vtrace 0xa 0xb rcx=0xVT`, `fieldrefs 0xa 0xb`. `vtreg` maps RTTI class → vtable VA; `readq` dumps vtable method ptrs; `vtrace` resolves virtual calls (seed reg=vtable). NOTE: `grep -v "^\["` eats `readq`'s `[k]` output — don't filter it.

## 4. NU-deconstruct — PUBLIC repo (operator rule: push EVERYTHING we deconstruct)

`github.com/chaosfox26/NU-deconstruct` (public). Holds the full WildStar64.exe
teardown + StsConnLib teardown + the tools + `login-protocol.md` findings. **RULE:
every bit we disassemble/RE gets documented and pushed here.** Feeds the
operator's special plan: **a NATIVE LINUX WildStar client** (never existed). The
`api-surface.tsv` = the Win32 replacement surface for that port. Local copies:
`Project Resources/Wildstar64-Deconstruct/`, `StsConnLib-Deconstruct/`.

## 5. THE LOGIN RE — the live thread (get the operator logged in)

Our clean engine reaches a REAL 16042 client: it connects to our STS (6600),
sends `/Sts/Connect` + `/Auth/LoginStart`; we look up the account (the operator's,
name redacted) in authdb, run SRP, reply. **Client throws "Unhandled NC Platform Error 15" and does
NOT proceed.** 5 attempts, all identical error (no client feedback: the client's
`Errors/` folder only dumps on a CRASH, not a login error).

**RE'd from StsConnLib (all in NU-deconstruct/StsConnLib64.MT.dll/login-protocol.md):**
- Flow: Connect → LoginStart → KeyData → RequestGameToken. Transport = HTTP-shaped text; reply matched to request by `s:` seq.
- Requests (captured live from the client vs OUR server): `<Connect>…</Connect>`; `<Request><LoginName>…</LoginName><NetAddress>…</NetAddress></Request>`.
- **KeyData blob = `[u32 LE len1][salt][u32 LE len2][B]`**, must consume the whole blob (parser `0x18002d4e0`, `cmp rax,rsi; jne error`).
- Handler chain: `CLoginStart` msg vtable `0x180125088`, **method[5]=`0x18000A320`** = LoginStart-reply handler. SRP client (`CSrpClient` vtable `0x18012CDB8`) is at **`[this+0x60]`**. `CSrpClient::method[5]=0x18002DE00` = state machine (`[srp+8]`), state 0 → `0x18002d4e0` (salt+B parse). It **validates B as a bignum, B<N** (`0x18002D60D`, `jns error`) → STS uses **standard OpenSSL SRP (big-endian)**, NOT the game-channel SRP variant.

**THE KEY UNSOLVED PIECES (why error 15), in priority order:**
1. **Reply ENVELOPE is wrong.** Client crash log (08/17, `realm-portable\clients\Wildstar\Errors\…260817…log`) leaked: `HandleRequestVerifiedIPList -- Could not find Items element <Reply type="array" />`. **STS replies are `<Reply type="…">` envelopes with typed sub-elements — NOT `<Content>`.** Every attempt used the wrong envelope, so the client likely fails at the ENVELOPE parse before ever reaching KeyData/SRP. **NEXT STEP: RE the exact `<Reply>` envelope + KeyData element schema (type attributes) from the client, don't guess.**
2. **KeyData encoding unresolved.** The base64 codec `0x18001F310` is called ONLY from the platform-init function `0x180018D90` (WSAStartup/disk) — NOT the login path. So KeyData is raw or a different encoding. Raw binary in XML is fragile (salt/B contain `<`/`&`). Small OpenSSL base64 fn `0x180081610` has crypto callers — maybe THAT decodes it. UNRESOLVED — RE which decode the reply path uses.
3. **B byte order** = big-endian (confirmed by the B<N validation).

**Our SRP is the WRONG variant for STS:** `src/NexusUnleashed.Cryptography/SRP6a.cs` uses `ReverseUInt32` (k,x,u) + little-endian `BigInteger.ToByteArray()` + block-reverse = WildStar GAME SRP. STS needs standard OpenSSL SRP (big-endian, standard k=H(N,g)). The account VERIFIER in authdb was made by the frozen realm's OpenSSL-SRP STS, so a standard SRP is needed to match it. **A correct standard-OpenSSL-SRP-6a for STS must be built** (separate from the game SRP).

**Attempts (all → error 15):** `<Reply>`+base64; `<Content>`+base64 (x2); `<Content>`+raw-bytes+big-endian-B. Current AuthFlow.cs WIP = raw KeyData + big-endian B (committed as WIP, doesn't work).

**TACTIC SHIFT (told operator): stop guess-and-retry. RE the exact reply schema
(envelope + encoding) to CERTAINTY from the client, THEN one retry.** The 08/17
crash log proves the login format is achievable (that client logged fully into the
frozen realm before an unrelated in-world crash).

## 6. STATE OF PLAY (what's running — IMPORTANT)

- **Our clean engine: UP** on 6600 (STS, capturing to `sts-capture.log`) / 23115 (auth, clear) / 24000 (world). Run from `src/NexusUnleashed.Realm/bin/Release/net10.0/NexusUnleashed.Realm.exe`, logs to `<scratch>/clean-engine.log`. `realm.json` there (gitignored, in bin/) has standard ports + `AuthDatabase` = authdb on 3307.
- **MariaDB: UP** on 3307, started STANDALONE by me: `database/bin/mariadbd.exe --no-defaults --datadir="…/realm-portable/data" --port=3307 --plugin-dir="…/database/lib/plugin" --bind-address=127.0.0.1`. (The bundled `data/my.ini` still points at the dead D: drive — always override datadir.)
- **Frozen realm: DOWN.** Operator ordered a FULL shutdown; I force-killed all `NexusUnleashed.*` servers + `mariadbd`. `servers/NexusUnleashed.StsServer/StsServer.json` reverted to port 6600 (clean). The Launcher app + `nxnode` (the logging host on 127.0.0.1:24950) left running.
- **Operator CANNOT PLAY** until the frozen realm is back up — and our engine + standalone MariaDB hold its ports (6600/23115/24000/3307). To let them play: kill our engine + our MariaDB, then they restart the realm via the launcher.
- Client: `realm-portable\clients\Wildstar` (WildStar64.exe). Launcher points it at `localhost` (from `realm-portable/launcher/data/config.json` Host=localhost) on the fixed ports — so it lands on OUR engine when the frozen realm is down.

## 7. Deferred / open
- `GetKeyFromTicket` (world key derivation) — re-source from the CLIENT (quarantined).
- World-entry payloads (0x0988/0x098B/0x0117/0x0262) — decoded in the capture, models pending (session earlier this day). `spec/protocol/world-entry.md`.
- The login (§5) is the gate to everything downstream.

---

## 8. LOGIN CRACKED END TO END — the client authenticates and reaches the realm (later 2026-08-19)

The real 16042 client now goes from the STS login all the way to the realm channel
and the "Retrieving Account Information" phase (full-color login screen — no more
error 15). Every step below is client-derived (its dispatch + its parsers) and our
DB; ZERO NexusForever source or NF-server captures (operator: "no NF servers", "no
NF protocol").

### 8.1 STS transaction chain (all working)
- `/Sts/Connect` → `/Auth/LoginStart` → `/Auth/KeyData` (SRP proof VERIFIES,
  game-SRP little-endian) → `/Auth/LoginFinish` → `/GameAccount/ListMyAccounts` →
  `/Auth/RequestGameToken` (`<Token>`). Post-SRP channel is ARC4(sessionKey).
- **ListMyAccounts FIX:** records are direct children of `<Reply>` — NO
  `<Items>`/type="array" wrapper (those strings don't exist in StsConnLib; its
  parser is `[reply+0x60]`=first record, `[item+0xc0]`=field). Enriched the
  GameAccount to the FULL field set the client reads (GameAccountId, AccountId,
  LoginName, UserId, UserName, Email, Alias, AccountAlias, GameCode, AppId,
  UserCenter, State, Status, Roles). A missing string field made WildStar64.exe
  `strlen(null)` → AccessViolation at RVA 0xB3885. Fixed → token issued.
- LoginFinish `AuthType` is the enum string `Password` (not `"1"`).

### 8.2 Realm channel (port 23115)
- Opens with a CLEAR `0x0003` hello (client accepts it), then speaks the auth-key
  encrypted container (`0x0244` in / `0x03DC` out). `WorldHandshake` bootstraps
  clear-then-container off `Crypt==null`.
- The client's `0x0244` decodes to inner op **`0x0592`** = realm-enter:
  `[build 16042][8B][login-name UTF-16][fields][client system survey]`. The live
  client uses `0x0592`, NOT the `0x058F` our earlier capture read.
- **Keying: the channel stays on the AUTH key after 0x0592** — the client's
  post-enter `0x0000` decodes with it, so no world re-key at character-select.

### 8.3 Character-list opcode = 0x0117 (cracked from the client dispatch)
- RE chain: Lua `HasReceivedCharacterList` → reads global `0x140C66DA8`+0x168 →
  the sole writer is the handler `0x140021540` (sets `[this+0x168]=1`, parses
  characters at STRIDE 0x330, fires the `CharacterList` Lua event) → its only
  `.text` xref is dispatch case `0x140021167` → walking the compare tree
  (`cmp r8d,0xe7; ja 0x210ce; sub 0x116; dec; je 0x21167`) gives opcode **0x117**.
- The dispatch function is `0x140020EA0`; opcode switch head at `0x140020EF1`.
  (Linear cumulative-sub enumeration of the tree is WRONG — subtractions are
  per-branch; trace each path.)
- `0x0117` is ALSO in our (retired) capture at 833B — it had been MISLABELED
  "player self block" in observed-opcodes.md; it is the character list.

### 8.4 Milestone proof + the current blocker
- Sending `0x0117` after `0x0592` took the client from the grayscale error-15
  screen to the full-color login with "Retrieving Account Information".
- BLOCKER: the client waits at that status for ACCOUNT-INFO message(s) before it
  paints character-select. One such message is `MaxCharacterLevelAchieved`
  (dispatch case `0x140020FF7`, writes `[G+0x16c]`), a sibling of the char-list
  case. NEXT: identify the account-info message(s) that clear the status (client
  dispatch), then generate `0x0117` from the client-derived layout + characterdb
  (target character = `character` id 22 on the test account, accountId 2).

### 8.5 Provenance discipline applied
- The captured `0x0117` body was a DIAGNOSTIC replay only, now RETIRED
  (`charlist-replay.bin` deleted, gitignored). The real path derives the layout
  from the client's parser and fills it from our characterdb.

---

## 9. Login-message dispatch MAP + dynamic-analysis tooling (later 2026-08-19)

### 9.1 The realm/account/character message dispatch (client-derived)
`WildStar64.exe` fn `0x140020EA0` = the realm-message handler `G::OnMessage(this,
arg, opcode(r8d), msg(r9))`; opcode switch head at `0x140020EF1`. A CFG trace
(tracking per-branch `sub`/`dec`/`cmp` on r8d — LINEAR cumulative-sub is wrong)
gives the full opcode → case → Lua-event map. All S->C messages the client
processes at login (validated: `0x117 → case 0x21167 → handler 0x21540`):

| opcode | Lua events / role |
|---|---|
| 0x036 | MaxCharacterLevelAchieved, CharacterDisabled, CharacterSelectFail (account+char constraints) |
| 0x0AD | SubscriptionExpired, GameTimeHoursRemaining, RealmTransferFlags |
| 0x0E7 | CharacterDisabled, CharacterSelectFail |
| 0x116 | (no strings) |
| **0x117** | **CHARACTER LIST** (handler 0x140021540, char stride 0x330) |
| 0x14B | (no strings) |
| 0x33D | SubscriptionExpired, GameTimeHoursRemaining, RealmTransferFlags |
| 0x36A | QueueFinished, TransferDestinationRealmList |
| 0x3E1 | RealmBroadcast, QueueFinished, TransferDestinationRealmList |
| 0x594/0x715/0x717/0x761/0x765/0x862 | (further realm msgs) |

The account-info messages gating "Retrieving Account Information" are in the
0x036/0x0AD/0x33D family. The char-list is a DESERIALIZED struct when the handler
runs (fixed offsets: +0x8 char vector, +0x18/+0x20 a string, +0x38, +0x40/+0x48,
+0x50, +0x5c), so the wire parse happens in the pump BEFORE dispatch (the dispatch
is a vtable method; ptr at .data 0x140C66D58).

### 9.2 Dynamic analysis: Frida tracer (the tool for the remaining formats)
Installed frida 17.17; `Project Resources\Tools-Working\Tools\re\ws-trace.py`
attaches to WildStar64.exe and hooks the dispatch (0x20EA0) + char-list handler
(0x21540) — VALIDATED (attaches, resolves base, installs hooks). Frida 17 API:
`Process.getModuleByName(name).base` (getBaseAddress removed). NEXT: a recv+Stalker
bootstrap (hook ws2_32!WSARecv, Stalker-trace the parse of a message that DOES
succeed — the 0x0003 hello — to locate the pump + the bit-reader), then trace the
0x117 deserializer's bit-reads = the exact wire layout, and GENERATE 0x117 from
characterdb. Pure client observation; no NF. Keep Stalker windows TIGHT (live game
thread; operator is playing — don't destabilize the client).

### 9.3 The 0x117 wire format is the hard next problem (honest state)
The char-list handler consumes a DESERIALIZED struct, so the wire parse is done by
the pump before dispatch. Locating that deserializer is non-trivial:
- STATIC: the {0x38,0x50,0x5c} struct-write signature matches 133 funcs (too broad);
  no per-message registry entry found by a naive u32-opcode search. The parse is
  likely schema/generic-reader driven, or the factory keys opcode as u16.
- DYNAMIC: a crash-probe (sending zero-body realm msgs 0x116/0x14B/0x036 to trigger
  the pump + backtrace) CRASHED the fragile client — REMOVED, do NOT repeat. Sending
  malformed messages to the live client destabilizes it (violates "PC takes priority").
SAFE PLAN for next: (a) find the pump via the G vtable (OnMessage ptr at .data
0x140C66D58) and read the per-opcode read path statically; OR (b) Frida-hook the
deserializer once found and either observe a genuinely-valid parse or NativeFunction-
call it in-process with a controlled buffer (sandbox — no network, no client-state
risk). Then build the GENERIC, account-keyed 0x117 generator from characterdb
(multi-account by design; reproducible; MIT — for the community).

---

## §10 — THE C++ PIVOT (2026-08-19, late) — full vision revealed

**Operator decision: port the entire clean engine to C++, 1:1, now.** The C# tree
becomes the byte-verified reference oracle (kept building until C++ overtakes it).

**What drove it — the full vision (bigger than a server):** merge the friend's C++
work; port to Linux; **heavy game-ENGINE work**; **new rendering features that never
existed — FSR 3/4, DLSS 3/4, and a DX12 renderer**; optimize for stability + community
content (quests/modding). The engine/renderer/DLSS/FSR/DX12 half is **unavoidably C++**
(SDKs, D3D hooking, the compiled proprietary engine, and the friend all live there), so
C++ becomes the center of gravity and the server unifies onto it. Operator is
optimization-first and wants one coherent native project.

**Language debate (recorded):** for a *pure server*, Rust ≈ C# > C++ on
fast+stable+strain (memory safety + no GC tail-latency; Rust = the optimization-nut
pick). The FULL scope flips it to C++. Rendering difficulty ladder: FSR1 easy
(post-process inject); FSR2/3/4 + DLSS3/4 need motion vectors the engine doesn't expose
(deep renderer RE); DX12 = renderer replacement. Multi-year summit — sequence it.

**Plan written:** `Claude/Context/CPP-PORT-PLAN.md` (decision, vision, component map,
dependency choices, Phase 0–4). Banners added to CONTINUE.md + STATE.md. Memory saved
([[the-cpp-pivot]]). Toolchain: CMake + vcpkg, C++20 (Asio coroutines), OpenSSL,
MySQL Connector/C++, nlohmann/json, spdlog, Catch2. **The specs + Frida RE tooling are
language-neutral and carry over unchanged.** The open protocol blocker (realm-enter →
char-select handshake / char-select C++ object G null) is unchanged and gets solved in
C++ in Phase 4.

**State of play at pivot:** engine (C#) still deployed and validated (char list parses
on the real client). Realm left as-is. Next action after context files: **Phase 0 — C++
project skeleton.** Repo layout still to confirm with operator (leaning `cpp/` subdir of
the current MIT repo).

---

## §11 — C++ PORT: Phases 0-2 (crypto + message model) DONE & byte-verified

Toolchain: VS 18 (2026), MSVC 19.51, C++20, CMake+Ninja bundled. **vcpkg manifest
mode** (`cpp/vcpkg.json`; vcpkg at `<home>\vcpkg`; toolchain file wired) —
OpenSSL 3.6.3 + asio installed & linked. Operator chose vcpkg-manifest for reproducible
deps. Build: `cmake -S cpp -B cpp/build -DCMAKE_TOOLCHAIN_FILE=<vcpkg>/scripts/
buildsystems/vcpkg.cmake` then `cmake --build cpp/build --config Release`; run
`cpp/build/Release/nexus_tests.exe`.

**Ported 1:1 + tested (ALL GREEN):**
- `net/bitstream.h` (PacketReader/Writer, LSB-first) — round-trip verified.
- `crypto/packet_crypt` — the cipher; **FINDING: Encrypt == EncryptForClient byte-for-byte**
  (register reversal cancels index reversal), so the S->C "direction" is a no-op; the real
  realm fix was CLEAR framing. Spec corrected.
- `net/frame.h` (GamePacketFrame), `net/world_packet` (container codec) — container
  round-trip proven.
- `proto/character_list` (0x0117 serializer) — 121 bytes for a single-character list, matches
  the validated bit-length exactly.
- `crypto/sts_srp` — WildStar game-SRP on OpenSSL bignum + SHA-256 (LE, ReverseUInt32,
  interleaved K); valid 128-byte B.
- `crypto/arc4.h` — clean-room RC4 (NOT copied), passes canonical KAT.
- `sts/sts_message` (StsRequest/StsReply/StsParser) — request parse, partial framing,
  reply "STS/1.0 200  OK" two-space form, back-to-back requests all verified.

**C# UNTOUCHED — still the deployed fallback/oracle.** Byte-verification discipline held.

**NEXT (Phase 2 finish -> Phase 3): the Asio async servers + AuthFlow + DB.**
- `StsServer`/`StsSession` (Asio TCP + ARC4 stream) + `AuthFlow` (the Connect/LoginStart/
  KeyData/LoginFinish/ListMyAccounts/RequestGameToken transaction using StsSrp) +
  `AuthSession`. Needs the DB store -> add **mysql** (libmariadb or mysql-connector-cpp)
  to vcpkg.json for `DbAccountStore`/`DbCharacterStore`.
- Then `GameServer`/`GameSession` (Asio) + `WorldHandshake` (Phase 3) + the char-list send.
- **Phase 2 milestone = real client authenticates against the C++ server end-to-end**
  (same Frida/driver verification as the C# side).

## §12 — C++ PORT AT PARITY WITH C# — verified against the LIVE client (2026-08-19)

**The real 16042 client authenticated end-to-end against the C++ engine and reached the
same state as the C# reference.** C++ server log:
```
[STS-SRP] proof VERIFIED (game-SRP little-endian)   <- SRP verified in C++
realm: <- 0x0592 realm-enter (396B)
realm: character-list provider: account 2 has 1 character(s)   <- DB read (libmariadb)
realm: -> 0x0117 character list (clear frame) for account 2 (121B)   <- validated serializer
realm: <- inner op=0x0000 (1B)
```
Client now sits at "Retrieving Account Information" — the SAME open blocker as C#
(char-select state object G null; char list dropped). Language-neutral; Phase 4.

**Full C++ stack now built + running + proven:** STS server + AuthFlow + SRP(OpenSSL) +
ARC4 + GameServer/GameSession(Asio coroutines) + WorldHandshake + DbAccountStore/
DbCharacterStore(libmariadb) + RealmConfig(nlohmann-json). Runs on the io_context across
hardware threads. `nexus_realm.exe` in cpp/build/Release (realm.json beside it, gitignored).

**Bug caught by the live client (byte-verify win #2):** the hello body was hand-typed with
the `0b14332f01` message-definitions stamp shifted 2 bytes early (byte 24 vs 26) ->
client error "Message Definitions Mismatch - Connection closed by remote host". Fixed to
match the C# HelloBodyHex byte-for-byte. (Win #1: Encrypt==EncryptForClient equivalence.)

**Phases 2+3 DONE in C++ (parity). Phase 4 = the realm-enter->char-select transition RE
(create G / register the 0x117 handler), now solved natively in C++.** C# fallback is
stopped only because C++ reused its ports; restart anytime.

## §13 — Account-retrieval barrier: deep RE + toolset (2026-08-19, autonomous)

C++ engine confirmed AT PARITY vs the live client. Pushed hard on the ONE remaining
blocker ("Retrieving Account Information"). Findings + full tool inventory:
`spec/protocol/account-retrieval-barrier.md`. Key results:
- Window-only screenshot (ws-shot.ps1) proved the client is on the **Login** screen with
  a `NetworkStatus` "Retrieving Account Information" overlay — waiting for account-data
  PUSH after 0x0592, BEFORE RealmSelect/Character. G-null is expected (pre-char-select).
- **Discovered `WS+0xEA3E0` = the Lua event-fire function** (event name in rdx) →
  `event-trace.py` logs every client event. The client fires only `NetworkStatus` then
  waits (no RealmListChanged/CharacterList).
- Captured the FULL message table (1121 opcodes, probe-all.json) + hello/char-list pump
  call-trees (pump-stalk.py). Descriptor fn18/fn20 are serialization only; semantic
  handlers dispatch via the active pre-game state.
- The router `WS+0x14f10` special-cases connection opcodes 0x3/0x76/0x3dc; 0x76 handler
  = WS+0x140015248 (candidate realm/account message, unresolved).
- NEXT: find the active Login/account pre-game state's message dispatcher (like char-select
  WS+0x20EA0) and enumerate its expected opcodes; OR event-trace + the engine's inject.txt
  to probe candidates SAFELY (never brute-force — malformed msgs have crashed the client).
State left clean: C++ server + client running; C# is history.
