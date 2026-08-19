# THE PRIVACY LAW

**Operator directive, 2026-08-19. Binary, enforced.**

Nothing pushed to the public repository may contain personal information: no
emails, no account names, no character names, no private/LAN IP addresses, no
local machine paths. This holds for code, comments, specs, commit messages,
docs, and any committed data.

## Where the risk is

The login and world captures contain real personal data — the account name, the
character name ("...Goldentail"), the client's LAN IP. When we pin a message
format from a capture, **use placeholders, never the real values**:

- account name → `<account>` / `accountName`
- character name → `<character>` / a neutral example like `Testchar`
- IP → `<addr>` / `127.0.0.1`
- token/session values → `<token>` / redacted

The captured *structure* (element names, field order, encoding) is what we pin —
the *values* stay out.

## Enforcement

- **Capture logs are gitignored** (`*.log`, `sts-capture*.log`, `captures/`, …) —
  they hold raw personal data and must never be committed.
- **`provenance/.private-terms`** is a LOCAL, gitignored list of the specific
  personal strings (account/character names). It lives only on the machine, so
  the names are never in the repo, not even in the checker.
- **`provenance/privacy-guard.py`** scans every git-tracked file for emails,
  private IPs, and any term in `.private-terms`, and fails before push on a hit.
  Run it in the gate alongside `nf-guard.py`.

## The check is clean-by-construction

Because the sensitive strings live only in the gitignored `.private-terms`, the
guard can enforce "these exact names must not appear" without ever publishing the
names themselves. Keep terms specific (full character name, not a shared surname)
so public game data (e.g. the creature "Goldentail King") does not false-trip.
