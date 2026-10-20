#!/usr/bin/env bash
# Runs only the live tests: the ones that call a real outside service (today the model of the judge and the simulated user,
# AGENTS.md section 3, "An outside service"). The gate leaves them out; run this when the model or
# the code that reads its answer changes, and after the model's server was updated.
#
# The model comes from the gitignored .env at the repo root (.env.example lists the names), as for `just server`.
# A live test with no model configured is reported as skipped, with the reason.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

cd "$ROOT"
if [[ -f .env ]]; then
  set -a
  # shellcheck disable=SC1091
  . ./.env
  set +a
fi
dotnet test --project Suwageval.Server.Tests --no-progress --filter-trait "live=true" "$@"
