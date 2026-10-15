#!/usr/bin/env bash
# End-to-end tests (agenteval_web/src/**/*.e2e.ts): a real browser against the real server and
# database. Humans run `just e2e`, CI runs this script. Arguments go to Playwright, e.g.
#   e2e.sh --headed        watch the browser
#   e2e.sh -g "health"     only the tests whose name matches
#
# Every run starts from an empty database of its own (agenteval_e2e), next to the development
# one, and its own server and web app (playwright.config.ts) — what you run by hand is not
# touched. Locally it starts PostgreSQL (just db); CI provides it as a service. A service the
# app gains later (a converter, a cache) gets started here the same way.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

if [ -z "${CI:-}" ]; then
    "$ROOT/scripts/.internal/db.sh" up >/dev/null
fi

# A fresh database for the run: the tests may then assume nothing but what they create. Two
# commands, not one: CREATE DATABASE cannot run inside the transaction a single -c would be.
recreate=(-c "DROP DATABASE IF EXISTS agenteval_e2e WITH (FORCE)" -c "CREATE DATABASE agenteval_e2e")
if [ -n "${CI:-}" ]; then
    psql -q "postgresql://agenteval:agenteval@localhost:5453/postgres" "${recreate[@]}"
else
    "$ROOT/scripts/.internal/db.sh" psql -q -d postgres "${recreate[@]}"
fi

cd "$ROOT/agenteval_web"
[ -d node_modules ] || pnpm install --frozen-lockfile --silent
exec pnpm exec playwright test -c e2e/playwright.config.ts "$@"
