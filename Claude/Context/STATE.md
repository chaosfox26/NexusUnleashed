# NexusUnleashed Engine — State of the Build

_Updated 2026-08-19 (protocol-capture milestone). Read `ARCHITECTURE.md` first._

## The situation

A clean-room WildStar (16042) server engine, built from the client, our data,
and the running realm as behavioral oracle. MIT, openly usable by anyone (no
credit required). Zero NF source ever opened; SRP from MIT Arctium, everything
else from the client / our data / the oracle's wire.

## BUILT + PROVEN (all pushed)

| layer | status |
|---|---|
| Crypto | SRP6a login proven end-to-end (9/9): register->B->(A,M1)->verify, keys agree, bad paths rejected. |
| Wire codec | Bit packer (12/12). **VALIDATED against REAL captured WildStar packets (7/7)** - parses opcode 0x0935 + guid out of the oracle's own movement stream. The codec matches Carbine's wire. |
| Framing | u32 LE self-inclusive size + u16 LE opcode, measured off the oracle, confirmed live. |
| **Protocol** | **157 opcodes PINNED from a live full-duplex capture** (41 C->S + 116 S->C, all decrypted): entity spawn/move/stats, world entry, combat, buffs, loot, emote. `spec/protocol/observed-opcodes.md` + `GameMessageOpcode` enum. |
| STS login | Text protocol from the client's StsConnLib; AuthFlow runs real SRP; live-socket login 7/7. |
| Client tables | All 384 tables -> typed models (generated); value reader cell-for-cell == our proven tbl_reader on core tables. Names every creature. |
| Accounts | DbAccountStore reads real SRP creds from the live authdb (5/5). |
| World sim | Entity + spatial grid + vision hysteresis (grid never misses, 200/200 vs brute force on 74k-entity world), movement + safety laws (200k steps zero NaN), Catmull-Rom patrols, faction/aggro (client dispositions, Mystpaw law), combat health. All worlds resident at once (2,729 in ~98 MB, 0.2 ms/tick). Living world runs on Arcterra (1,755 creatures, 600 ticks, zero NaN). |
| Content | The restoration loads (263,756 spawns / 65 worlds) - NOTE: inherited the frozen realm's current corruption (dupes, over-population, faction scramble); clean re-export is task #46, deferred until serving. |
| Host + deploy | Runnable realm host (boots as NexusUnleashed, our MotD). Self-contained linux-x64 ELF publish + systemd + install docs. |

## The capture pipeline (how the protocol was pinned)

Our own diagnostics tap (`packetdump=1`) in the frozen realm records every
message's opcode + bytes - C->S after the client's crypto, S->C before
encryption - so both directions are decrypted. `CaptureAnalyzer` turns the dump
into an opcode inventory. The operator plays; we observe the wire (a fact). The
tap sits before the realm's handlers, so a WIP oracle never limits coverage.

## NOW: message models (the current phase)

The codec is validated; the next work is pinning each message's field layout
from the capture and building typed models the world layer sends/receives.
Started with the movement broadcast. Critical path: world-entry sequence
(0x0988/0x0981/...), entity create (0x0262), position broadcast (0x0935), then
combat/inventory/quest. Occasional targeted captures from the operator ("do one
thing so I can isolate that message"); the bulk works from the capture on hand.

## Then

World server host (sim + message layer) -> a real client connects and sees a
living world -> parity harness -> NU-Linux deploy. Content re-export (task #46)
before it serves for real.
