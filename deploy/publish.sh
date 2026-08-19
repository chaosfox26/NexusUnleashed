#!/usr/bin/env bash
# Publish the realm host self-contained for Linux and stage a deployable bundle.
# Usage: deploy/publish.sh [outDir]   (default: ./publish)
set -euo pipefail
cd "$(dirname "$0")/.."
OUT="${1:-publish}"
RID=linux-x64

echo "== publishing NexusUnleashed.Realm ($RID, self-contained) =="
dotnet publish src/NexusUnleashed.Realm -c Release -r "$RID" \
  --self-contained true \
  -p:PublishSingleFile=true -p:PublishTrimmed=false \
  -o "$OUT"

echo "== staging content + config =="
cp -r content "$OUT/content"
cp deploy/realm.json "$OUT/realm.json"
cp deploy/nexusunleashed.service "$OUT/nexusunleashed.service"
cp deploy/INSTALL.md "$OUT/INSTALL.md"

echo "== done: $OUT =="
ls -la "$OUT" | sed -n '1,20p'
echo
echo "Next: rsync $OUT/ to the VPS /opt/nexusunleashed, edit realm.json (DB password),"
echo "install the systemd unit, and \`systemctl enable --now nexusunleashed\`."
