# Spec: character create (client 0x5CD5 → server 0xDC result)

**Status: opcode + semantics PINNED from the client decompile (2026-08-20).
Exact wire bit-layout of 0xDC still to confirm live.**

## Provenance
Derived entirely from the client (IDA decompile) — the char-select manager's
message dispatcher and the create sender/result handlers. No emulator source.

- `sub_140020EA0` — char-select manager message dispatcher. Switches on the
  **wire opcode** (`a3`). Confirmed mapping (a3 == wire opcode, since a3==279==0x117
  is CharacterList, which our engine already speaks and the client accepts):
  | opcode | dec | handler | meaning |
  |---|---|---|---|
  | 0x116 | 278 | sub_140023230 | (char-select preamble) |
  | **0x117** | 279 | sub_140021540 | **CharacterList** (already implemented) |
  | 0x36 | 54 | inline | MaxCharacterLevelAchieved |
  | **0xDC** | 220 | sub_140021FB0 | **CharacterCreateResult** |
  | 0xE6 | 230 | inline | CharacterDelete |
  | 0xE7 | 231 | inline | CharacterDisabled |
  | 0x12E | 302 | inline | CharacterSelectFail |
  | 0x14B | 331 | sub_140022190 | (char-select, state 3) |
  | 0x33D | 829 | sub_1400225E0 | (char-select) |

- `sub_140023E90` — the CREATE SENDER (client → server). Builds internal
  sub-messages 603/604, serializes to the create request (wire **0x5CD5**, ~298 B:
  name + race/class/path/sex/faction + appearance). On a valid local build it sets
  the **pending flag `qword_140C66DA8 + 368 = 1`**. On local validation failure it
  fires Lua `CharacterCreateFailed` and never sends.

- `sub_140021FB0` — the CREATE RESULT HANDLER (server → client, opcode 0xDC):
  ```
  if (mgr[+368] == 1) {          // a create is pending
      mgr[+368] = 0;             // clear pending
      if (msg[+12] == 3) {       // RESULT CODE 3 == SUCCESS
          // look up the new character (by id) in the already-received list,
          // set state mgr[+40]=4, stash char id mgr[+552]=msg[+0],
          // and begin ENTERING THE WORLD with it.
      } else {
          Apollo_LUAEvent(.., "CharacterCreateFailed", ..);
          // msg[+12]==6 → error 143523 (name taken?), else 143525 (generic)
      }
  }
  ```

## The create-result message (0xDC) — in-memory struct (wire layout TBC)
- `+0`  : u64 — **new character id**
- `+12` : u32 — **result code** (3 = success, 6 = name-conflict-ish, else generic fail)

These are post-deserialization struct offsets. The exact **wire** bit-packing is
not yet pinned (the message-definition table, not a code literal). Since it is a
tiny message, the working hypothesis for the wire is: `u64 charId` then the result
word. **Confirm live** by watching the client accept-or-reject the first 0xDC.

## The server sequence on a successful create (derived)
The success branch looks the new character up **in the client's current list**, so
the list must already contain it. Order:

1. Persist the new character to characterdb.
2. Send **0x117 CharacterList** refreshed (now includes the new char) → client adds
   it and learns its id (`sub_140021540`).
3. Send **0xDC CharacterCreateResult** with `result=3` and the new char id.
4. Client transitions to **world entry** with that character.

## The wall this leads to
Create success does **not** rest at the char screen — the client immediately begins
world entry (same funnel as selecting an existing character and hitting Enter Game).
So a correct 0xDC lands the client at the **world-load wall**: it needs the world
server (map load + entity spawns + movement), which is the world-entry sequence
already captured in `spec/protocol/world-entry.md`. The create-result is a small
prerequisite; the world server is the real gate to "standing in the world."
