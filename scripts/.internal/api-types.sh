#!/usr/bin/env bash
# TypeScript types for the web app, generated from the server's OpenAPI document. The C# records
# the endpoints return are the single source of truth; this file is never edited by hand.
#
#   api-types.sh           regenerate Suwageval.Web/src/api/openapi.d.ts
#   api-types.sh --check   fail if the committed file is out of date (the gate runs this)
#
# The document is written by the build (Microsoft.Extensions.ApiDescription.Server), which starts
# the app only up to the point where its routes are known — no database needed.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
TARGET="$ROOT/Suwageval.Web/src/api/openapi.d.ts"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# The generating target is skipped while its cache in obj/ is newer than the binary; the
# document has to be written into this run's directory every time.
rm -f "$ROOT/Suwageval.Server/obj/Suwageval.Server.OpenApiFiles.cache"
dotnet build "$ROOT/Suwageval.Server" --nologo -v quiet \
    -p:OpenApiGenerateDocumentsOnBuild=true -p:OpenApiDocumentsDirectory="$TMP" >"$TMP/build.log" ||
    { cat "$TMP/build.log" >&2; exit 1; }

cd "$ROOT/Suwageval.Web"
[ -d node_modules ] || pnpm install --frozen-lockfile --silent
pnpm exec openapi-typescript "$TMP/Suwageval.Server.json" --output "$TMP/schema.d.ts" >/dev/null

if [ "${1:-}" = "--check" ]; then
    if ! diff -q "$TMP/schema.d.ts" "$TARGET" >/dev/null 2>&1; then
        echo "Suwageval.Web/src/api/openapi.d.ts is out of date with the server's API — run: just api-types" >&2
        exit 1
    fi
    echo "API types up to date"
else
    cp "$TMP/schema.d.ts" "$TARGET"
    echo "${TARGET#"$ROOT"/}"
fi
