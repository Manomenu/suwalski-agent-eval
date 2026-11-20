#!/usr/bin/env bash
set -euo pipefail
usage() {
    cat <<'USAGE'
End-to-end tests (Suwageval.Web/src/**/*.e2e.ts): a real browser against the real server and database. Not part of
the gate (suwgate check). Humans run `just e2e`, CI runs this script. Arguments go to Playwright, e.g.
  e2e.sh --headed        watch the browser
  e2e.sh -g "health"     only the tests whose name matches

Every run starts from an empty database of its own (suwageval_e2e; a linked worktree gets a suffixed name and its own
ports, suwgate dev checkout-env), next to the development one, and its own server and web app (playwright.config.ts):
what you run by hand is not touched. Locally it starts PostgreSQL (suwgate dev db up); CI provides it as a service.
The server is built once here, so the web server of the tests starts the built binary instead of compiling in its
own start-up window.
USAGE
}
if [ "${1:-}" = "--help" ]; then
    usage
    exit 0
fi
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SUWGATE="$ROOT/Suwageval.Web/node_modules/.bin/suwgate"
[ -x "$SUWGATE" ] || (cd "$ROOT/Suwageval.Web" && pnpm install --frozen-lockfile --silent)
eval "$("$SUWGATE" dev checkout-env)"

if [ -z "${CI:-}" ]; then
    "$SUWGATE" dev db up >/dev/null
fi

recreate_outside_one_transaction=(-c "DROP DATABASE IF EXISTS $E2E_DATABASE WITH (FORCE)" -c "CREATE DATABASE $E2E_DATABASE")
if [ -n "${CI:-}" ]; then
    psql -q "postgresql://suwageval:suwageval@127.0.0.1:5463/postgres" "${recreate_outside_one_transaction[@]}"
else
    "$SUWGATE" dev db psql -q -d postgres "${recreate_outside_one_transaction[@]}"
fi

dotnet build "$ROOT/Suwageval.Server" --nologo -v q

cd "$ROOT/Suwageval.Web"
exec pnpm exec playwright test -c e2e/playwright.config.ts "$@"
