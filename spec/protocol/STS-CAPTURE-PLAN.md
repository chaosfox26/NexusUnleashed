# STS login capture — the turnkey plan (to finish login, cleanly)

**Goal:** capture one normal login's clear-text STS exchange so the login XML
shape + the SRP KeyData byte layout get pinned from the client's OWN bytes — the
last missing piece for login. Passive, non-disruptive, no NF source touched.

## How it works

The client connects to `localhost:6600` (STS). We slip a **passive capture proxy**
in between: it listens on 6600, forwards untouched to the real STS moved to 6601,
and logs every byte both ways. The login succeeds exactly as normal; we just watch.

```
client → 127.0.0.1:6600 (proxy, raw log) → 127.0.0.1:6601 (real STS) → login OK
```

Only STS (6600) needs this — the auth (23115) and world (24000) channels are
already captured. After STS login the client talks to those directly, unaffected.

## Steps (once)

1. **Move the realm's STS port** — in
   `realm-portable\servers\NexusUnleashed.StsServer\StsServer.json`, change
   `"Port": 6600` → `"Port": 6601`.
2. **Restart the realm** from the launcher (picks up 6601; frees 6600).
3. **Run the proxy** (Claude starts this; it waits for 6600 to free, then binds):
   ```
   NexusUnleashed.CaptureProxy.exe 6600 127.0.0.1 6601 sts-login-capture.log raw
   ```
4. **Log in normally.** The client hits 6600 → proxy → 6601. Login completes.
5. Hand `sts-login-capture.log` to Claude → the STS schema is pinned, login gets
   wired into the clean engine.
6. **Revert**: set `StsServer.json` back to `6600`, restart. Done.

## Why this is the clean, effective route

- **Complete in one login:** both requests AND the correct replies (incl. the
  exact KeyData encoding) captured at once — no guess-and-iterate.
- **Non-disruptive:** you play/log in normally; the realm is otherwise untouched.
- **Cleanest provenance:** pure wire observation of the client's behavior
  (source #3) — no NF code read. The recovered/NF STS handler stays closed.
- **Loopback-safe:** the proxy is in the data path, so it sees the bytes without
  the unreliable Windows localhost packet-sniffing.
