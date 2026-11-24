#!/bin/bash
# Copy the latest Unity WebGL build into the local site and bump the cache-busting stamp.
set -euo pipefail

SRC="$HOME/Emberclad/Builds/WebGL/Build"
SITE="$HOME/Emberclad/WebsiteDev"
DST="$SITE/website/play/Build"
PORT=8787

cd "$SITE"
mkdir -p "$DST"

echo "Source build files:"
ls -laT "$SRC"

cp -p "$SRC"/* "$DST"/

fail=0
for f in "$SRC"/*; do
  n=$(basename "$f")
  a=$(shasum -a 256 "$f" | awk '{print $1}')
  b=$(shasum -a 256 "$DST/$n" | awk '{print $1}')
  if [ "$a" = "$b" ]; then echo "ok    $n  $a"; else echo "MISMATCH $n"; fail=1; fi
done
[ $fail -eq 0 ] || { echo "Checksum mismatch, stopping."; exit 1; }

STAMP=$(TZ=UTC stat -f %Sm -t %Y%m%d-%H%M%S "$SRC/WebGL_staging.data.br")
sed -E -i '' "s/var BUILD = '[^']*'/var BUILD = '$STAMP'/" website/index.html
grep -q "var BUILD = '$STAMP'" website/index.html || { echo "Could not set BUILD stamp in index.html"; exit 1; }
echo "New BUILD stamp: $STAMP"

check() { curl -s "http://localhost:$PORT/" | grep -q "$STAMP"; }

for i in 1 2 3 4 5; do check && break; sleep 1; done
if check; then
  echo "Dev server is serving the new stamp."
else
  echo "Dev server not serving new stamp; restarting wrangler dev on $PORT..."
  pkill -f "wrangler.*dev" || true
  nohup npx wrangler dev --port $PORT --local > wrangler-dev.log 2>&1 &
  for i in $(seq 1 30); do check && break; sleep 1; done
  if check; then echo "Dev server restarted and serving the new stamp."; else echo "Dev server still not serving new stamp; see wrangler-dev.log"; exit 1; fi
fi
