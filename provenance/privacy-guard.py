#!/usr/bin/env python3
"""privacy-guard - keep personal info out of the public repo (operator rule).

Scans git-TRACKED files for personal information that must never be published:
emails, private IP addresses, and any term listed in the LOCAL, gitignored file
`provenance/.private-terms` (one term per line - account names, character names,
etc.). The terms file is never committed, so the sensitive strings live only on
the machine, not in the checker.

Run before every push (and in the gate). Exit 0 = clean, 1 = personal info found.
"""
import os
import re
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

EMAIL = re.compile(r"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")
# Private / LAN IPv4 ranges (RFC1918) + loopback-with-context is fine, flag LAN.
PRIVATE_IP = re.compile(r"\b(?:10\.\d{1,3}\.\d{1,3}\.\d{1,3}|192\.168\.\d{1,3}\.\d{1,3}|172\.(?:1[6-9]|2\d|3[01])\.\d{1,3}\.\d{1,3})\b")
# Local Windows user-home paths leak the account username. Flag `X:\Users\<name>` and the
# claude-project `C--Users-<name>-` form, but NOT our own <user>/<home> placeholders.
LOCAL_PATH = re.compile(r"(?:[A-Za-z]:[\\/]Users[\\/]|C--Users-)(?!<user>|<home>|<)[^\\/\s\"'`)>]+")
# Allowed: the project's own commit-author placeholder.
ALLOW_EMAIL = {"noreply@anthropic.com"}
# RFC 2606 / 6761 reserved TLDs + example domains: reserved for docs/tests, can
# never be a real personal address, so synthetic test fixtures using them are OK.
ALLOW_EMAIL_TLDS = (".test", ".invalid", ".example", ".localhost")
ALLOW_EMAIL_DOMAINS = ("example.com", "example.org", "example.net")

def email_allowed(addr):
    a = addr.lower()
    if a in ALLOW_EMAIL:
        return True
    domain = a.split("@", 1)[1] if "@" in a else ""
    return domain.endswith(ALLOW_EMAIL_TLDS) or domain in ALLOW_EMAIL_DOMAINS

def tracked_files():
    out = subprocess.run(["git", "ls-files"], cwd=REPO, capture_output=True, text=True)
    return [l for l in out.stdout.splitlines() if l.strip()]

def load_terms():
    path = os.path.join(REPO, "provenance", ".private-terms")
    if not os.path.isfile(path):
        return []
    terms = []
    for line in open(path, encoding="utf-8", errors="replace"):
        t = line.strip()
        if t and not t.startswith("#"):
            terms.append(t)
    return terms

def main():
    terms = [t.lower() for t in load_terms()]
    hits = []
    for rel in tracked_files():
        path = os.path.join(REPO, rel)
        # skip binary-ish and this guard's own docs mentioning the patterns
        if rel.endswith((".png", ".jpg", ".jpeg", ".gif", ".bin", ".ico", ".pdf")):
            continue
        try:
            with open(path, encoding="utf-8", errors="replace") as f:
                for i, line in enumerate(f, 1):
                    low = line.lower()
                    for m in EMAIL.finditer(line):
                        if not email_allowed(m.group(0)):
                            hits.append((rel, i, f"email {m.group(0)}"))
                    for m in PRIVATE_IP.finditer(line):
                        hits.append((rel, i, f"private IP {m.group(0)}"))
                    for m in LOCAL_PATH.finditer(line):
                        hits.append((rel, i, f"local user path {m.group(0)}"))
                    for t in terms:
                        if t in low:
                            hits.append((rel, i, f"private term ({len(t)} chars)"))
        except OSError:
            pass
    if not hits:
        print(f"privacy-guard: CLEAN - no personal info in {len(tracked_files())} tracked files"
              + (f" (checked {len(terms)} private terms)" if terms else " (no .private-terms list present)"))
        return 0
    print("privacy-guard: PERSONAL INFO FOUND in tracked files - do NOT push:\n")
    for rel, i, what in hits:
        print(f"  {rel}:{i}: {what}")
    print(f"\n{len(hits)} hit(s). Redact before committing. See provenance/PRIVACY.md.")
    return 1

if __name__ == "__main__":
    sys.exit(main())
