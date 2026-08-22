# Tasks — clean C++ engine (world-entry → in-world polish)

_Snapshot 2026-08-22. Authority for the active item: `Claude/Context/CONTINUE.md`._

## 🔶 Active
- **#8 — Standing pose: make the player render upright and movable.** She loads into the arkship as a
  full clothed body, but renders **lying down + movement-locked** in every world (measured live — not
  the intro). Ruled out (measured): HP/death (live 250/250), player-bind (works), stand-state (+440=0),
  +4896 (velocity blend), spline node (red herring), camera (works — only the character is locked).
  Key finding: an **unbound copy** of the entity also lies → lying is the default idle for any
  0x0262-created entity, and her **model animation controller appears frozen** (emotes flip the flag
  but don't move her body). Entity data verified correct via the client's own reader sub_140096FA0.
  **NEXT:** run `tools/live-probes/anim_tick.py` — is the per-frame anim update ticking for her unit?
  If not, find why a world-spawned entity's animation set doesn't link (char-select force-loads the
  same model and stands).

## ⏳ Pending (queued in-world polish)
- **#12 — Full HUD data pass** (stats / health / unit frame). HP is a placeholder; wire real stats via
  unit-property ids.
- **#11 — Abilities on the action bar (LAS + ability book).** Bar art is back but slots are empty.
  Ability-book add works (0x111 loc-type-4); the LAS/ActionSetLib bar is a separate system — find the
  LAS-assign server message + correct class-7 (Spellslinger) starter spell ids. (`ActionSetLib` Lua
  binding = sub_140758630.)
- **#9 — Client persistence layer** (UI / keybinds / options / addon SavedVariables) mapping.

## ✅ Done (world-entry chain)
- #1 Capture live 0x5CD5 create-character bytes
- #2 Find the create-result response opcode + layout
- #3 Persist created character to characterdb
- #4 Decode creationId → race/class/sex/faction from CharacterCreation.tbl
- #5 Fix char-select rendering (appearance + outfit) — black silhouette
- #6 World entry: fix the ~30s post-enter disconnect (0x0845 keepalive)
- #7 Render the player body (0x0262 race/sex/customization + item visuals)
- #10 Reconcile all client→server opcodes (handle everything the UI can send)

## Standing directives (always in force)
Continuous mode; NO NF / no corpus (client + our data only); C++ not C#; straight code only (notes in
`cpp/docs`); full privacy sweep (`python provenance/privacy-guard.py`, EXIT=0) before every commit;
never push unless asked. Operator grant: drive the client autonomously (launch/login/enter).
