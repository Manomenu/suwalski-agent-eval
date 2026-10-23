#!/usr/bin/env bash
# Sourced by check.sh and e2e.sh: gives a linked git worktree its own test databases and e2e ports,
# so checkouts that run the gate or the browser tests at the same time on one machine do not collide.
# The main checkout and CI keep the plain names and ports (suwageval_test, suwageval_e2e, 6231/3231).
# A linked worktree takes a suffix from its directory name (lower-case [a-z0-9_], at most 20 chars) for
# the database names and a port pair (20000-29999) from a stable hash of that name. Nothing is cleaned
# up afterwards: a worktree's databases stay until you drop them. A value already in the environment wins.
#
# The builds leave nothing running behind them: by default MSBuild keeps worker processes (node reuse) and its server
# alive for the next build. The gate, the e2e run and every agent worktree each started their own set, and a day of
# them held gigabytes of RAM doing nothing. A build of the gate is a few seconds slower without them.
export MSBUILDDISABLENODEREUSE=1
export DOTNET_CLI_USE_MSBUILD_SERVER=0
_checkout_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
_git_dir="$(git -C "$_checkout_root" rev-parse --absolute-git-dir)"
_common_dir="$(git -C "$_checkout_root" rev-parse --path-format=absolute --git-common-dir)"
if [ -z "${CI:-}" ] && [ "$_git_dir" != "$_common_dir" ]; then
    _name="$(basename "$_checkout_root")"
    _suffix="$(printf '%s' "$_name" | tr '[:upper:]' '[:lower:]' | tr -c 'a-z0-9_' '_' | cut -c1-20)"
    _base=$((20000 + $(printf '%s' "$_name" | cksum | cut -d' ' -f1) % 5000 * 2))
    export E2E_DATABASE="${E2E_DATABASE:-suwageval_e2e_$_suffix}"
    export TEST_CONNECTION_STRING="${TEST_CONNECTION_STRING:-Host=localhost;Port=5463;Database=suwageval_test_$_suffix;Username=suwageval;Password=suwageval}"
    export E2E_SERVER_PORT="${E2E_SERVER_PORT:-$_base}"
    export E2E_WEB_PORT="${E2E_WEB_PORT:-$((_base + 1))}"
    echo "checkout $_name: test database suwageval_test_$_suffix, e2e database $E2E_DATABASE, e2e ports $E2E_SERVER_PORT/$E2E_WEB_PORT" >&2
fi
export E2E_DATABASE="${E2E_DATABASE:-suwageval_e2e}"
export E2E_SERVER_PORT="${E2E_SERVER_PORT:-6231}"
export E2E_WEB_PORT="${E2E_WEB_PORT:-3231}"
unset _checkout_root _git_dir _common_dir _name _suffix _base
