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
