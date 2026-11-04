#!/usr/bin/env bash
# Why: sourced by check.sh and e2e.sh: a linked worktree gets its own test databases and e2e ports (a stable hash of its directory name), so checkouts running at once do not collide; a value already in the environment wins
export MSBUILDDISABLENODEREUSE=1
export DOTNET_CLI_USE_MSBUILD_SERVER=0
_checkout_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
_git_dir="$(git -C "$_checkout_root" rev-parse --absolute-git-dir)"
_common_dir="$(git -C "$_checkout_root" rev-parse --path-format=absolute --git-common-dir)"
if [ -z "${CI:-}" ] && [ "$_git_dir" != "$_common_dir" ]; then
    _name="$(basename "$_checkout_root")"
    _hash="$(printf '%s' "$_name" | cksum | cut -d' ' -f1)"
    # Why: worktrees of one repository share a long prefix, so the hash keeps their databases apart; 30 + 11 characters stay under PostgreSQL's 63
    _suffix="$(printf '%s' "$_name" | tr '[:upper:]' '[:lower:]' | tr -c 'a-z0-9_' '_' | cut -c1-30)_$_hash"
    _base=$((20000 + _hash % 5000 * 2))
    export E2E_DATABASE="${E2E_DATABASE:-suwageval_e2e_$_suffix}"
    export TEST_CONNECTION_STRING="${TEST_CONNECTION_STRING:-Host=localhost;Port=5463;Database=suwageval_test_$_suffix;Username=suwageval;Password=suwageval}"
    export E2E_SERVER_PORT="${E2E_SERVER_PORT:-$_base}"
    export E2E_WEB_PORT="${E2E_WEB_PORT:-$((_base + 1))}"
    echo "checkout $_name: test database suwageval_test_$_suffix, e2e database $E2E_DATABASE, e2e ports $E2E_SERVER_PORT/$E2E_WEB_PORT" >&2
fi
export E2E_DATABASE="${E2E_DATABASE:-suwageval_e2e}"
export E2E_SERVER_PORT="${E2E_SERVER_PORT:-6231}"
export E2E_WEB_PORT="${E2E_WEB_PORT:-3231}"
unset _checkout_root _git_dir _common_dir _name _hash _suffix _base
