#!/usr/bin/env bash
set -euo pipefail
usage() {
    cat <<'USAGE'
The server's OpenAPI document and the web app's TypeScript types generated from it. The C# records
the endpoints return are the single source of truth; neither output is ever edited by hand.

  api-types.sh   regenerate contract/openapi.json and Suwageval.Web/src/api/openapi.d.ts
The gate's api-contract step fails when either committed file is out of date.

contract/openapi.json is the contract the CLI, the web and agents' tooling read, with its version in contract/package.json: committed, so that the gate's api-version step
compares it with the main branch and a consumer in another repository can take it from a backend commit. The
first line of openapi.d.ts names the sha256 of the document it was generated from: a consumer can tell
which contract its types come from without a package registry.

The document is written by the build (Microsoft.Extensions.ApiDescription.Server), which starts
the app only up to the point where its routes are known — no database needed.
USAGE
}
if [ "${1:-}" = "--help" ]; then
    usage
    exit 0
fi
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CONTRACT="$ROOT/contract/openapi.json"
TARGET="$ROOT/Suwageval.Web/src/api/openapi.d.ts"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

force_the_document_to_be_written_again() { rm -f "$ROOT/Suwageval.Server/obj/Suwageval.Server.OpenApiFiles.cache"; }
force_the_document_to_be_written_again
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

cp "$TMP/Suwageval.Server.json" "$CONTRACT"
cp "$TMP/openapi.d.ts" "$TARGET"
echo "${CONTRACT#"$ROOT"/}"
echo "${TARGET#"$ROOT"/}"
