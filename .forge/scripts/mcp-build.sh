#!/bin/bash
# mcp-build.sh — runs in the .NET SDK pod (see dotnet-pod.sh) with the build context at /work:
# restore against Served.SDK $(cat SDK_VERSION) from the Forge feed, build at $VERSION, pack
# Served.MCP as a .NET tool into /work/out, then install that package and make it answer an MCP
# handshake.
set -euo pipefail
WORK="${WORK:-/work}"
cd "$WORK"
SDK_VERSION=$(cat SDK_VERSION)
COMMIT=$(cat COMMIT)
FEED="${FEED_URL:?FEED_URL not set}"
echo "dotnet $(dotnet --version) — Served.MCP $VERSION (commit ${COMMIT:0:7}) against Served.SDK $SDK_VERSION"

# Served.* comes from the Forge feed, everything else from nuget.org.
cat > nuget.config <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="ServedForge" value="$FEED/v3/index.json" allowInsecureConnections="true" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
    <packageSource key="ServedForge"><package pattern="Served.*" /></packageSource>
  </packageSourceMapping>
</configuration>
XML

PROJECT=Served.MCP/Served.MCP.csproj
PINS=("-p:ServedSdkVersion=$SDK_VERSION" -p:NuGetAudit=false)
PROPS=(-c Release "-p:Version=$VERSION" "-p:ServedMcpCommit=$COMMIT" "${PINS[@]}")

echo "── restore"
dotnet restore "$PROJECT" --configfile "$WORK/nuget.config" "${PINS[@]}"

echo "── build"
dotnet build "$PROJECT" --no-restore "${PROPS[@]}"

echo "── pack"
dotnet pack "$PROJECT" --no-build "${PROPS[@]}" -o "$WORK/out"
ls -l "$WORK/out"

# Served.MCP has no test project of its own (its tests are part of ServedApp's Served.Tests). The
# gate here is the package itself: installed as a tool it must start and answer initialize and
# tools/list over stdio — without credentials and without reaching an API.
echo "── tool install + MCP handshake"
cat > tool.nuget.config <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="out" value="$WORK/out" />
  </packageSources>
</configuration>
XML
rm -rf "$WORK/tool"
dotnet tool install Served.MCP --tool-path "$WORK/tool" --version "$VERSION" --configfile "$WORK/tool.nuget.config"

REQUESTS='{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"pipeline","version":"1"}}}
{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
set +e
printf '%s\n' "$REQUESTS" | HOME="$WORK/tool-home" SERVED_API_URL=http://127.0.0.1:9 SERVED_TRACING_ENABLED=false \
  timeout 60 "$WORK/tool/served-mcp" > handshake.out 2> handshake.err
CODE=$?
set -e
tail -5 handshake.err || true
SERVER_VERSION=$(grep -o '"serverInfo":{[^}]*}' handshake.out | head -1 || true)
TOOLS=$(grep -o '"inputSchema"' handshake.out | wc -l | tr -d ' ')
echo "exit $CODE — ${SERVER_VERSION:-no serverInfo} — $TOOLS tools"
if [ "$CODE" != "0" ] || [ -z "$SERVER_VERSION" ] || [ "$TOOLS" -lt 1 ]; then
  echo "✗ The installed tool did not answer the MCP handshake"
  head -c 2000 handshake.out
  exit 1
fi
if ! grep -q "$VERSION" <<<"$SERVER_VERSION"; then
  echo "✗ The tool reports another version than $VERSION"
  exit 1
fi
rm -rf "$WORK/tool" "$WORK/tool-home" handshake.out handshake.err
