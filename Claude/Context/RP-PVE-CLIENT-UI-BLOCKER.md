# RP-PvE realm type — PARKED, needs client-UI / CDN work (operator-flagged 2026-08-20)

**Goal (operator):** the realm should read **RP-PvE** (and RP-PvP as a first-class type), a full
restoration of the RP realm-type branch that the shipped 16042 client does not have.

## Why it can't be done server-side
The 16042 client's realm-type is **hardcoded to PvE/PvP** in its own pre-game UI:
- `CodeEnumRealmPVPType` enum has only `PVE`(0) and `PVP`(1) (marked `Incomplete`).
- `RealmSelect.lua` (list Type column), `CharacterSelect.lua` (char-window "Realm: X (PvE)"),
  `AccountServices.lua`, `Character.lua` all do `nRealmPVPType == PVP and "PvP" or "PvE"` — anything
  that isn't PvP renders "PvE". No RP branch, no RP flag read from the realm struct.
- The realm-list `PvpType` field IS 2 bits on the wire (so 0..3 fit; `2=RP-PvE`, `3=RP-PvP` is the
  natural convention), and the server can send 2 — but the client will still DISPLAY it as "PvE"
  until its Lua has an RP branch.

## Why the client edit is a whole project (THE BLOCKER)
The pre-game UI is **not loose files** — there are ZERO loose `.lua` in `C:\Games\Wildstar`. The
entire stock UI is packed inside **`Patch\ClientData.archive` (13 GB)** (`Knowledge\client-ui` is only
an EXTRACTED reference — editing it does nothing to the running client). Pre-game UI also can't be
overridden by an in-game addon (addons load post-login). So showing "RP-PvE" requires:
1. Unpack ClientData.archive → edit RealmSelect.lua + CharacterSelect.lua + AccountServices.lua +
   Character.lua (add the RP branch + fix the PvE/PvP filters so RP-PvE files under PvE, RP-PvP under
   PvP) → repack with correct index/checksums, **or**
2. Do it at the **CDN level** and redistribute a patched client.
Either way it is archive/CDN tooling + redistribution, not a code change here. **Operator explicitly
flagged the CDN edit as "a whole mess on its own."**

## The exact edits (ready for when the archive path exists)
Convention: `nRealmPVPType` 0=PvE, 1=PvP, **2=RP-PvE, 3=RP-PvP**.
- Server: set `RealmEntry.PvpType = 2` for Evindra (one line in world_handshake.cpp realm-list build).
- `RealmSelect.lua` type display (~382): 4-way — 1→PvP, 2→"RP-PvE", 3→"RP-PvP", else PvE.
- `RealmSelect.lua` `FilterForPvE` (~175): keep `type==0 or type==2`; `FilterForPvP` (~187): `type==1 or type==3`.
- `CharacterSelect.lua` (~303, ~331), `AccountServices.lua` (~465), `Character.lua` (~838): same 4-way string.
Cleanest: one helper `GetRealmTypeString(nType)` used in all five spots.

## Tooling (identified 2026-08-20 — NF-free)
`.archive`/`.index` read (and write is the hard part): `_Tools\MarbleBag-NexusVault-CLI` (+ NexusVault
C#/Java), `_Tools\Cromon-wildstar-studio` (classic browser/extractor),
`Project Resources\bezgelor\tools\archive_extractor\archive_extractor.py`. **AVOID
`_Tools\NexusForever-Nexus.Archive` / `voidwatch-NexusForeverContainer` (NF).** Extraction is
well-supported; **repacking** ClientData.archive (13 GB) with a valid index + block hashes so the
client still loads it is the real work — treat as its own focused project (server-side data flag is
already set: PvpType=2). Operator (2026-08-20): **"only one true way to do it"** — the full client
restoration IS wanted; this is not to be left cosmetic.

## Status
PARKED for a dedicated run (archive repack). Operator committed to doing it the true way. Server side
is restoration-ready (Evindra sends PvpType=2 = RP-PvE). Everything ELSE the operator asked (realm
name Evindra, realm-list populate, Enter-Realm re-entry, level field, appearance render, delete) is
server-side and DONE/verifying.
