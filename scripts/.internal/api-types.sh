#!/usr/bin/env bash
set -euo pipefail
if [ "${1:-}" = "--help" ]; then
    cat <<'HELP'
The server's OpenAPI document and the web app's TypeScript types generated from it. The C# records
the endpoints return are the single source of truth; neither output is ever edited by hand.

  api-types.sh           regenerate contract/openapi.json and Suwageval.Web/src/api/openapi.d.ts
  api-types.sh --check   fail if either committed file is out of date (the gate runs this)

contract/openapi.json is the contract the CLI, the web and agents' tooling read, with its version
in contract/package.json: committed, so that api-diff.sh can compare it with master. The first
line of openapi.d.ts names the sha256 of the document it was generated from.

The document is written by the build (Microsoft.Extensions.ApiDescription.Server), which starts
the app only up to the point where its routes are known — no database needed.
HELP
    exit 0
fi
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CONTRACT="$ROOT/contract/openapi.json"
TARGET="$ROOT/Suwageval.Web/src/api/openapi.d.ts"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# Why: the generating target is skipped while its cache in obj/ is newer than the binary; the document has to be written into this run's directory every time
rm -f "$ROOT/Suwageval.Server/obj/Suwageval.Server.OpenApiFiles.cache"
dotnet build "$ROOT/Suwageval.Server" --nologo -v quiet \
    -p:OpenApiGenerateDocumentsOnBuild=true -p:OpenApiDocumentsDirectory="$TMP" >"$TMP/build.log" ||
    { cat "$TMP/build.log" >&2; exit 1; }

cd "$ROOT/Suwageval.Web"
[ -d node_modules ] || pnpm install --frozen-lockfile --silent
pnpm exec openapi-typescript "$TMP/Suwageval.Server.json" --output "$TMP/schema.d.ts" >/dev/null
digest="$(sha256sum "$TMP/Suwageval.Server.json" | cut -d' ' -f1)"
{
    echo "// Generated from contract/openapi.json (sha256 $digest) by scripts/.internal/api-types.sh; never edit."
    cat "$TMP/schema.d.ts"
} >"$TMP/openapi.d.ts"

if [ "${1:-}" = "--check" ]; then
    stale=()
    if ! diff -q "$TMP/Suwageval.Server.json" "$CONTRACT" >/dev/null 2>&1; then stale+=("contract/openapi.json"); fi
    if ! diff -q "$TMP/openapi.d.ts" "$TARGET" >/dev/null 2>&1; then stale+=("Suwageval.Web/src/api/openapi.d.ts"); fi
    if [ ${#stale[@]} -gt 0 ]; then
        echo "${stale[*]} out of date with the server's API — run: just api-types" >&2
        exit 1
    fi
    echo "API contract and types up to date"
else
    mkdir -p "$(dirname "$CONTRACT")"
    cp "$TMP/Suwageval.Server.json" "$CONTRACT"
    cp "$TMP/openapi.d.ts" "$TARGET"
    echo "${CONTRACT#"$ROOT"/}"
    echo "${TARGET#"$ROOT"/}"
fi
