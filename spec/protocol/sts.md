# Spec: STS login protocol (auth handshake)

**Status: framing + message set PINNED from the client binary; body schemas UNPINNED.**

## Provenance (the derivation — reproducible)

Every fact below was extracted 2026-08-19 from Carbine's own client STS library:

> `Wildstar Clients\CDN\Install\Client64\StsConnLib64.MT.dll` (1,899,520 bytes,
> the 16042 install), by scanning printable strings and MSVC RTTI type names.

No emulator source was consulted. The strings are facts about the protocol the
client speaks; RTTI names (`CLoginStart`, `CKeyDataTxnNotify`, ...) are the
client's own transaction classes, embedded by Carbine's compiler.

## Confirmed live (2026-08-19, oracle serving, operator in-game)

Re-measured against the running realm while a client was logged in: auth :23115
opens `size=53 op=0x0003`, world :24000 opens `size=59 op=0x03DC` (both
byte-identical to the first capture), and **STS :6600 is client-speaks-first**
(no server hello — the client sends the first STS request). AuthFlow is built for
exactly that (client sends LoginStart first).

## Framing (PINNED)

STS is a **text protocol, HTTP-shaped**. Measured tokens: `STS/1.0`, `POST`,
`ERROR`, `Reply`, header keys `l:` / `s:` (and uppercase variants `L:` / `S:`),
`X-Sts-Connect`, `Reply-To`, and the body wrapper tag `<Content>`.

```
POST /<Service>/<Message> STS/1.0
l:<body length in bytes>
s:<sequence number>

<xml body>
```

Replies mirror the shape (`STS/1.0 <code> <text>`, same `l:`/`s:` headers, XML
body). `ERROR` marks failure replies.

## Services (PINNED — measured string set)

`Sts`, `Auth`, `Presence`, `GameAccount`, `Friend`, `Mail`

## Messages (PINNED — measured strings + RTTI transaction classes)

| message | RTTI evidence | role in login |
|---|---|---|
| `Connect` | `X-Sts-Connect` header | opens the STS session |
| `Ping` | string | keepalive |
| `LoginStart` | `CLoginStart`, `CLoginStartTxnNotify` | client sends account name; server replies with SRP salt + B |
| `KeyData` | `CKeyDataTxnNotify`, string `KeyData` | SRP exchange (client A, proof M1) |
| `LoginFinish` | `CLoginFinish`, `IsRequireLoginFinish` | completes SRP, session established |
| `RequestGameToken` | `CRequestGameToken` | client obtains the game token handed to the realm |
| `ConsumeGameToken` | `CConsumeGameToken` | server side redeems the token |
| `LoginTokenStart` | `CLoginTokenStart`, `TokenKeyData` | token-based (relogin) variant of LoginStart |

Additional transactions present in the client (not needed for first login):
`PresenceLogin`, `PresenceLogout`, `PresenceGetUserInfo`, `PresenceSetAppData`,
`AuthGetUserInfo`, `ListMyAccounts`, `RequestIpToken`, `VerifyIpToken`,
`AddIp`, `CheckIp`, `Geolocation`, `GetUserContact`, `FriendSendMessage`,
`Mail`, `VerifySecondaryAuth`, `VerifySecondaryPassword`, `VerifyMobiOtp`,
`AssociateMyExternalAccount`.

The SRP implementation is compiled in (`Services\Srp\Srp.cpp` in the library's
own PDB paths) — SRP6a, matching our `NexusUnleashed.Cryptography.SRP6a`.

## UNPINNED (awaiting one oracle capture)

- **XML body schemas** — element/attribute names inside `<Content>` for each
  message. Not recoverable from static strings (built dynamically); one capture
  of a login against the frozen realm pins them.
- **Reply status codes** — `200 OK` shape assumed from the HTTP form; confirm.
- **Whether `l:` counts body bytes only** — confirm from a captured frame.

Pin procedure: capture client→realm STS traffic at login (the STS port is
clear-text before SRP completes), transcribe the first session here, flip the
markers. The ledger entry names the capture file, not any source code.
