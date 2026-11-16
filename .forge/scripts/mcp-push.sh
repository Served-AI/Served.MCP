#!/bin/bash
# mcp-push.sh — runs in the .NET SDK pod (see dotnet-pod.sh): pushes /work/out/*.nupkg to the
# Forge NuGet feed with the Forge registry credential from the regcred secret (/feed/config.json).
# The credential never reaches the log.
set -euo pipefail
FEED="${FEED_URL:?FEED_URL not set}"
HOST=$(sed -E 's#^https?://([^/]+).*#\1#' <<<"$FEED")
AUTH=$(tr -d ' \n\t\r' < /feed/config.json | grep -o "\"$HOST\":{[^}]*}" | grep -o '"auth":"[^"]*"' | cut -d'"' -f4 || true)
if [ -z "$AUTH" ]; then
  echo "✗ The regcred secret has no credential for $HOST"
  exit 1
fi

shopt -s nullglob
PKGS=(/work/out/*.nupkg)
if [ ${#PKGS[@]} -eq 0 ]; then
  echo "✗ Nothing to push in /work/out"
  exit 1
fi
for PKG in "${PKGS[@]}"; do
  CODE=$(curl -sS -o /tmp/push.out -w '%{http_code}' -m 300 -X PUT \
    -H "Authorization: Basic $AUTH" -F "package=@$PKG" "$FEED/v3/package" || echo 000)
  if [ "$CODE" != "201" ] && [ "$CODE" != "200" ]; then
    echo "✗ Push of $(basename "$PKG") answered HTTP $CODE: $(head -c 300 /tmp/push.out)"
    exit 1
  fi
  echo "✓ $(basename "$PKG") → $FEED (HTTP $CODE)"
done
