#!/usr/bin/env python3
"""nf-guard - the baked-in No-NF enforcement (provenance/NO-NF.md).

Mechanically scans the clean engine's CODE for any reference into the
NexusForever-derived trees (the decompiled `recovered/` realm, the shipped
assemblies, the NF corpus). Any hit is a provenance failure and fails the build.

This catches textual references. It CANNOT catch a formula/layout that was
learned by reading NF and retyped with no NF text left behind - that is caught by
discipline and the ledger, not by grep. Run this in the gate; keep it green.

Usage:  python provenance/nf-guard.py        (from the repo root)
Exit 0 = clean, 1 = contamination found.
"""
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Directories whose CODE must never reference the NF trees.
SCAN_DIRS = ["src", "test", "tools"]
SCAN_EXT = (".cs", ".csproj", ".fs", ".py")

# Forbidden reference patterns - paths/names that only appear if code reaches
# into an NF-derived tree. Deliberately specific so ordinary English ("recovered
# the key from the capture") in a clean comment does NOT trip it.
FORBIDDEN = [
    r"realm-source[\\/]+recovered",
    r"recovered[\\/]+NexusUnleashed",      # the decompiled-NF namespace tree
    r"realm-portable[\\/]+servers",
    r"NexusForever",
    r"Project Resources[\\/]+_AllRepos",
    r"_ForkPool",
    r"_Emulators-",
]
# Note: we deliberately do NOT match bare prose like "from NF" - anti-NF comments
# ("pinned from the client, not the NF code") are legitimate and would false-trip.
# The reliable contamination signal is a PATH/NAME reference into an NF tree above.
PATTERN = re.compile("|".join(FORBIDDEN), re.IGNORECASE)

def scan():
    hits = []
    for d in SCAN_DIRS:
        root = os.path.join(REPO, d)
        if not os.path.isdir(root):
            continue
        for dirpath, dirnames, filenames in os.walk(root):
            # skip build output
            dirnames[:] = [x for x in dirnames if x not in ("bin", "obj")]
            for fn in filenames:
                if not fn.endswith(SCAN_EXT):
                    continue
                path = os.path.join(dirpath, fn)
                try:
                    with open(path, encoding="utf-8", errors="replace") as f:
                        for i, line in enumerate(f, 1):
                            if PATTERN.search(line):
                                rel = os.path.relpath(path, REPO)
                                hits.append((rel, i, line.strip()))
                except OSError:
                    pass
    return hits

def main():
    hits = scan()
    if not hits:
        print("nf-guard: CLEAN - no references into any NF-derived tree.")
        return 0
    print("nf-guard: PROVENANCE FAILURE - code references NF-derived material:\n")
    for rel, i, text in hits:
        print(f"  {rel}:{i}: {text}")
    print(f"\n{len(hits)} reference(s). See provenance/NO-NF.md. Build is tainted.")
    return 1

if __name__ == "__main__":
    sys.exit(main())
