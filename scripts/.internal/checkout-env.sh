#!/usr/bin/env bash
# Why: sourced by check.sh and e2e.sh: a linked worktree gets its own test databases and e2e ports (a stable hash of its directory name), so checkouts running at once do not collide; a value already in the environment wins
_not_sourced="checkout-env.sh sets variables in the shell that sources it: run 'source scripts/.internal/checkout-env.sh' from bash or zsh, or run Suwageval.Gate/scripts/check.sh"
# Why: the script finds its checkout from its own path, which only bash (BASH_SOURCE) and zsh (%x) give; without it a linked worktree silently got the main checkout's databases
if [ -n "${BASH_VERSION:-}" ]; then
    if [ "${BASH_SOURCE[0]}" = "$0" ]; then
        echo "$_not_sourced" >&2
        exit 1
    fi
    _self="${BASH_SOURCE[0]}"
elif [ -n "${ZSH_VERSION:-}" ]; then
    case "${ZSH_EVAL_CONTEXT:-}" in
        *file*) ;;
        *)
            echo "$_not_sourced" >&2
            return 1
            ;;
    esac
    # shellcheck disable=SC2296 # Why: zsh's own way to name the file being sourced; bash never expands this branch
    _self="${(%):-%x}"
else
    echo "checkout-env.sh: this shell gives a sourced script no way to find its own path; $_not_sourced" >&2
    return 1 2>/dev/null || exit 1
fi
unset _not_sourced
export MSBUILDDISABLENODEREUSE=1
export DOTNET_CLI_USE_MSBUILD_SERVER=0
if [ -z "${CI:-}" ]; then
    _checkout_root="$(cd "$(dirname "$_self")/../.." && pwd)"
    if ! _git_dir="$(git -C "$_checkout_root" rev-parse --absolute-git-dir)" ||
        ! _common_dir="$(git -C "$_checkout_root" rev-parse --path-format=absolute --git-common-dir)"; then
        echo "checkout-env.sh: $_checkout_root is not a git checkout, so a linked worktree cannot be told from the main one: source the file of a cloned repository or worktree" >&2
        unset _self _checkout_root _git_dir _common_dir
        return 1
    fi
    if [ "$_git_dir" != "$_common_dir" ]; then
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
fi
export E2E_DATABASE="${E2E_DATABASE:-suwageval_e2e}"
export E2E_SERVER_PORT="${E2E_SERVER_PORT:-6231}"
export E2E_WEB_PORT="${E2E_WEB_PORT:-3231}"
unset _self _checkout_root _git_dir _common_dir _name _hash _suffix _base
