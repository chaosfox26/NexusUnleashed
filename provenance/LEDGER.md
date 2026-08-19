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
