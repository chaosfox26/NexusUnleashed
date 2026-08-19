# Provenance Ledger

Every component of this engine names its clean source. Sources rank:
1 client · 2 our data/knowledge · 3 the behavioral oracle (frozen realm) ·
4 permissive code (MIT/Apache/BSD). NF source is last resort; entries must name
a non-NF source wherever possible.

| component | class | source | evidence | notes |
|---|---|---|---|---|
| (audit of the frozen tree) | reference | provenance-audit.py | audit-frozen-tree.tsv | A 9.1% / B 13.8% / C 77% — the migration map |
| NexusUnleashed.Cryptography/SRP6a.cs | 4-permissive | Arctium WildStar-Server | MIT, Copyright (c) 2018 Arctium; header retained; namespace retargeted | SRP6a handshake; standard algorithm |
| NexusUnleashed.Cryptography/ARC4.cs | 4-permissive | Arctium WildStar-Server | MIT; header retained | stream cipher (RC4) |
| NexusUnleashed.Cryptography/Adler32.cs | 4-permissive | Arctium WildStar-Server | MIT; header retained | checksum |
| NexusUnleashed.Cryptography/Extensions.cs | 4-permissive | Arctium WildStar-Server | MIT; header retained; byte helpers used by SRP6a | |
| NexusUnleashed.Cryptography/Rng.cs | A-authored | this project | CSPRNG key gen; algorithm is a fact | replaces Arctium Helper (banner/logging dropped) |
| NexusUnleashed.Network/PacketReader.cs | A-authored | this project | bit-packed wire is a client FACT (our datamine); bit-stream algorithm is standard | LSB-first reader |
| NexusUnleashed.Network/PacketWriter.cs | A-authored | this project | mirror of the reader | |
| NexusUnleashed.Network/IGamePacket.cs | A-authored | this project | opcode identity is a protocol fact | message contract |
| test/NexusUnleashed.Network.Tests/RoundTrip.cs | A-authored | this project | 12/12 pass — parity discipline at the bit level | |
| NexusUnleashed.Network/GamePacketFrame.cs | A-authored | this project + spec/protocol/frame.md | envelope SHAPE authored; header widths UNPINNED until oracle capture | honest placeholder |
| NexusUnleashed.Network/GameSession.cs | A-authored | this project | modern .NET Pipelines transport; not Arctium's socket web | |
| NexusUnleashed.Network/GameServer.cs | A-authored | this project | async acceptor + opcode handler table | |
| spec/protocol/frame.md | spec | oracle (to be captured) | the clean method to pin the frame; source is the frozen realm, NOT NF | |
| NexusUnleashed.GameData/GameTableReader.cs | A-authored | our datamine + tbl_reader.py | .tbl binary format is OUR documented spec (equiv-gated 10.27M values); read Creature2 = 53,137 rows/173 fields correctly | header offsets pinned from our Python |
| test/NexusUnleashed.GameData.Tests/ReadTable.cs | A-authored | this project | verifies against a real client table | |

## src/NexusUnleashed.Sts/ (2026-08-19)

| file | class | source |
|---|---|---|
| `StsMessage.cs` | AUTHORED | STS framing measured from the client's own `StsConnLib64.MT.dll` (see `spec/protocol/sts.md` for the extraction); parser/framer code written fresh |
| `StsServer.cs` | AUTHORED | our own async listener/router; standard .NET sockets |
| `AuthFlow.cs` | AUTHORED | flow order from client RTTI transaction classes (facts); body schemas marked UNPINNED pending an oracle capture |
| `spec/protocol/sts.md` | AUTHORED | derivation document — every token named with its extraction method |

No emulator source was consulted for this layer. The one non-client input is
our own SRP6a (MIT Arctium seed, already ledgered under Cryptography).

## src/NexusUnleashed.Content/ + content/ (2026-08-19)

| file | class | source |
|---|---|---|
| `Tsv.cs`, `WorldContent.cs` | AUTHORED | our own native content format and loader, written fresh |
| `content/spawns.tsv` | OUR DATA | exported read-only from the running realm's worlddb (263,756 rows) — the restoration campaign's own work product (client-derived placements, zone-forge, NUSE, hand-built casts) |
| `content/patrols.tsv` | OUR DATA | entity→Spline2 patrol wires (8,059), authored by the restoration's patrol passes; spline nodes themselves live in the client tables |
| `content/kits.tsv` | OUR DATA | 20,020 creature-spell entries from the retail-kit restoration (Jabbithole/wiki/patch-note derivation, boss-kit-mapper) |

The loader proof requires the load to equal the live DB's own counts exactly.

## Framing PINNED (2026-08-19)

`GamePacketFrame` / `spec/protocol/frame.md`: header widths measured from the
behavioral oracle (the running frozen realm), not from any source. Auth port
23115 and world port 24000 each opened with a self-inclusive u32 LE size + u16
LE opcode; two independent frames agreed byte-for-byte with the layout. Raw
captures preserved in the spec. This is a wire measurement — observing bytes on
a socket is not reading code.

## src/NexusUnleashed.Database/ (2026-08-19)

| file | class | source |
|---|---|---|
| `DbAccountStore.cs` | AUTHORED | our own `IAccountStore` over the authdb `account` table (id/email/s/v/gameToken/sessionKey) — schema is our own DB, SQL and hex handling written fresh |
| dependency: MySqlConnector 2.3.7 | THIRD-PARTY (MIT) | permissive async MySQL driver |

Proven read-only against the live authdb: salt 16 bytes, verifier 128 bytes,
unknown account -> null (5/5). No emulator source consulted.

## SRP6a login proven (2026-08-19)

| file | class | source |
|---|---|---|
| `SrpServer.cs` | AUTHORED | public server-side driver + the verification decision over the SRP6a primitive; our own code |
| `SrpReferenceClient.cs` | AUTHORED | reference SRP client + `ComputeVerifier` (account registration), mirroring the primitive's WildStar parameters (N, g, k, blockwise reverse, interleaved session key). Written fresh from the SRP6a math, not from any server. |

The SRP6a primitive itself is the MIT Arctium seed (already ledgered). The full
login round trip is proven (9/9): register -> B -> (A, M1) -> verify -> session
keys AGREE; wrong password, A=0, and tampered M1 all rejected.

## AuthFlow wired to real SRP (2026-08-19)

`AuthFlow.cs` now runs the proven SRP6a state machine per session: LoginStart
computes B from the account's salt+verifier, KeyData verifies the client's A+M1
and derives the session key, RequestGameToken gates on authentication. XML body
layout still UNPINNED (SRP values carried as hex in <Content> until one oracle
capture); the flow and crypto are real. Proven over a live TCP socket end to
end (7/7): a real client ran SRP against the STS server, got M2 and a token;
wrong password rejected over the wire.

## src/NexusUnleashed.GameData.Gen + .Generated (2026-08-19)

| file | class | source |
|---|---|---|
| `GameData.Gen/TableCodeGen.cs`, `Program.cs` | AUTHORED | code generator: reads a client `.tbl` schema (a fact) and emits a typed C# record + reader. Our own code. |
| `GameData.Generated/*.g.cs` (384 files) | GENERATED | one typed model per client table, produced mechanically from Carbine's own column definitions. Not hand authored; regenerate from the client. |

`GameTableReader.ReadSchema` added (schema-only, immune to row-layout quirks).
All 384 tables generate (1.9s) and compile; typed load proven on the core
tables (Creature2 53,137 / Spell4 66,383 / World 2,729 incl. 990 + 3335 /
Quest2 5,194). This is the architecture's "facts -> generated" path: the whole
GameTable layer is now typed, from the client, zero NF.

## GameTableReader value reads file-true (2026-08-19)

`GameTableReader.Read` now applies the structural string-pad mask ported from
our own `tbl_reader.py` (equivalence-gated to the engine's dumps), with a
per-row record-arithmetic assertion. Proven: 322 tables read model-free
(1,946,807 rows), and 10 core tables (Creature2, Spell4, Spell4Effects 131,010,
Prerequisite 32,131, Quest2, World, WorldZone, MapZone, Creature2DisplayInfo,
TaxiNode) are CELL-FOR-CELL EQUIVALENT to tbl_reader.py. The 62 model-bound
tables (WorldSky/WorldWater*/ColorShift/Item2/...) are the SAME class our proven
Python reader also skips without a model - not a defect, a documented limit.

## TextTable + GameDataService (2026-08-19)

| file | class | source |
|---|---|---|
| `GameData/TextTable.cs` | AUTHORED | reads Carbine's localization .bin (id->string), format ported from our own tbl_reader.read_text_table. **539,251 strings, byte-for-byte equivalent to the proven reader.** |
| `GameData.Generated/GameDataService.cs` | AUTHORED | loads + indexes the core tables and text, typed lookups (CreatureName etc.) for the world layer |

Proven (6/6): 53,137 creatures / 66,383 spells / 2,729 worlds indexed; text
resolution EXACTLY matches tbl_reader+read_text_table (49,603/53,131 named
creatures resolve; the rest carry empty client strings). The engine names
"Firestorm Bomber", "Eldan Teleporter", etc. from the client alone.
