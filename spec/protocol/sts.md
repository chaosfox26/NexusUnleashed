# Spec: STS login protocol (auth handshake)

**Status: framing + message set + body FIELD NAMES PINNED from the client binary;
only the exact XML shape / KeyData binary layout await one clean login capture.**

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

## Body field names — PINNED from the client binary (2026-08-19, clean)

The message field names ARE present as static string literals in
`StsConnLib64.MT.dll` (the client's reply parser references the element names it
reads). Extracted by `grep -aoE` over the library — clean source #1, no NF:

| message | fields (client string literals) |
|---|---|
| `LoginStart` (req) | `LoginName` / `email` (the account identifier) |
| `LoginStart` (reply) | `KeyData` (carries the SRP salt + B) |
| `KeyData` (req) | `KeyData` (carries the client A + proof M1) |
| `KeyData` (reply) | `KeyData` (carries the SRP server proof M2) |
| `LoginFinish` (reply) | `LocationId`, `UserId`, `UserCenter`, `UserName`, `AccessMask` |
| `RequestGameToken` (reply) | `Token` |

Also present: `Salt`, `Verifier`, `Password`, `M1`, `M2` (the SRP terms). The
client's SRP is standard **OpenSSL SRP** (`crypto/srp/srp_lib.c`, `ssl/tls_srp.c`
in the library's own paths) — matches our `SRP6a`.

## Reply format — RE'd from the CLIENT (2026-08-19, clean, zero NF)

Derived by disassembling `StsConnLib64.MT.dll` with our own tool
(`tools/client-re/sts_re.py`, capstone+pefile, MIT) — analysis of Carbine's
client (source #1). No emulator/NF source consulted. The **request** formats are
also confirmed from a live capture of the client against our own STS server (the
client's own bytes):

- **`/Sts/Connect` (req):** `<Connect><ConnType>…</ConnType><Address>…</Address>
  <ProductType>…</ProductType><AppIndex>…</AppIndex><Epoch>…</Epoch>
  <Program>…</Program><Build>…</Build><Process>…</Process></Connect>`. Server
  replies with an empty `200 OK` (the client proceeds on it — confirmed).
- **`/Auth/LoginStart` (req):** `<Request><LoginName>…</LoginName>
  <NetAddress>…</NetAddress></Request>`, with a `p:` header of session params.
- **`/Auth/LoginStart` (reply):** carries a **`<KeyData>` element, base64**. The
  client's handler (at `.text` 0x0A360) does `GetField("KeyData")` →
  **base64-decode into a ≤256-byte buffer** → feeds the SRP setup. So the reply
  body is `…<KeyData>base64(blob)</KeyData>…` where the blob is the SRP salt+B.
- **`/Auth/KeyData` (req):** the same handler then **builds** a
  `<Request><KeyData>base64(A+M1 blob)</KeyData></Request>` — symmetric encoding.
- A **second auth path** exists (fields `ServerRand`, `ServerPublicKey`,
  `ServerSignature`) — the token/signed-challenge variant (LoginTokenStart),
  separate from the SRP email/password path.

The full STS field dictionary is a literal table in `.rdata` (0x1801243B0+):
KeyData, LoginName, NetAddress, Salt, Verifier, ServerRand, ClientRand, Token,
UserId, UserName, UserCenter, LocationId, AccessMask, PasswordHash, Content,
Request, Reply, … — the message vocabulary, straight from the client.

## STILL being pinned (RE in progress)

- **The KeyData blob's internal byte layout** — how salt and B are delimited
  inside the base64 blob (length-prefixing width/order). Being traced through the
  client's SRP/SocketCrypt setup; will be stated from the client, not guessed and
  not from NF. (Values in any example use placeholders — see PRIVACY.md.)

## Historical UNPINNED note

The **exact wire shape** around those field names is not a static string and is
the last piece:

- Are the fields XML child elements of `<Content>` or attributes?
- The **binary encoding inside `KeyData`** — the SRP values are carried in one
  `KeyData` blob (very likely base64 of length-prefixed binary), but the exact
  layout (length width/endianness, salt-then-B order) must come from the client's
  bytes, not guessed.
- Reply status shape (`200 OK`), and whether `l:` counts body bytes only.

**Pin procedure (clean):** the STS port is clear-text before SRP completes, so
either (a) point the client at OUR STS — `StsServer.RequestObserver` logs every
request to `sts-capture.log` — or (b) capture a normal login on 6600. Transcribe
here, flip the markers. The ledger names the capture file, never any NF source.
The recovered/NF STS handler is OFF LIMITS for this (No-NF law) even though it
would show the same shape — we take it from the client's bytes.
