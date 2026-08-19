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
1. **Reply ENVELOPE is wrong.** Client crash log (08/17, `C:\Games\Wildstar\Errors\…260817…log`) leaked: `HandleRequestVerifiedIPList -- Could not find Items element <Reply type="array" />`. **STS replies are `<Reply type="…">` envelopes with typed sub-elements — NOT `<Content>`.** Every attempt used the wrong envelope, so the client likely fails at the ENVELOPE parse before ever reaching KeyData/SRP. **NEXT STEP: RE the exact `<Reply>` envelope + KeyData element schema (type attributes) from the client, don't guess.**
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
- Client: `C:\Games\Wildstar` (WildStar64.exe). Launcher points it at `localhost` (from `realm-portable/launcher/data/config.json` Host=localhost) on the fixed ports — so it lands on OUR engine when the frozen realm is down.

## 7. Deferred / open
- `GetKeyFromTicket` (world key derivation) — re-source from the CLIENT (quarantined).
- World-entry payloads (0x0988/0x098B/0x0117/0x0262) — decoded in the capture, models pending (session earlier this day). `spec/protocol/world-entry.md`.
- The login (§5) is the gate to everything downstream.
