#!/usr/bin/env bash
set -euo pipefail
if [ "${1:-}" = "--help" ]; then
    cat <<'HELP'
Does this change break the server's API contract, and does its version say so? Compares
contract/openapi.json of the working tree with the one on a base ref (default: master, or
origin/master when master is not a local branch). Installed CLIs and agents' tooling lag behind the
server, so a changed document needs a new version in contract/package.json: a higher one for a
compatible change, a higher major for a breaking change (a removed endpoint or field, a narrowed
type, a new required parameter). The full gate runs it.

  api-diff.sh [base-ref]   exit 1 on a change the version does not account for, 2 when the base ref
                           is missing or a document cannot be read, 3 when oasdiff is not installed,
                           0 otherwise (also when the base has no contract yet)

Uses oasdiff (https://github.com/oasdiff/oasdiff): `oasdiff breaking base revision --fail-on ERR`.
Run api-types.sh first when the server changed, so that the working tree's document is current.
HELP
    exit 0
fi
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CONTRACT="contract/openapi.json"
PACKAGE="contract/package.json"

base="${1:-}"
if [ -z "$base" ]; then
    if git -C "$ROOT" show-ref --verify --quiet refs/heads/master; then base=master; else base=origin/master; fi
fi

if ! command -v oasdiff >/dev/null 2>&1; then
    echo "api-diff.sh: oasdiff is not installed (https://github.com/oasdiff/oasdiff/releases, or: brew install oasdiff)." >&2
    exit 3
fi

if ! git -C "$ROOT" rev-parse --verify --quiet "$base^{commit}" >/dev/null; then
    echo "api-diff.sh: $base is not a commit here (a typo, or not fetched yet: git fetch origin). Pass the base ref as the first argument." >&2
    exit 2
fi

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
if ! git -C "$ROOT" show "$base:$CONTRACT" >"$TMP/base.json" 2>/dev/null; then
    echo "api-diff.sh: no $CONTRACT on $base yet; nothing to compare against."
    exit 0
fi
if git -C "$ROOT" diff --quiet --no-index "$TMP/base.json" "$ROOT/$CONTRACT" 2>/dev/null; then
    echo "API contract unchanged since $base"
    exit 0
fi

# Why: with --fail-on, a document that cannot be read exits like a breaking change; a run without it tells them apart
if ! oasdiff breaking "$TMP/base.json" "$ROOT/$CONTRACT" >"$TMP/load.log" 2>&1; then
    cat "$TMP/load.log" >&2
    echo "api-diff.sh: oasdiff could not compare the documents (see above); is $CONTRACT valid JSON? Run: just api-types" >&2
    exit 2
fi
version_of() { python3 -c 'import json,sys; print(json.load(sys.stdin)["version"])'; }
now="$(version_of <"$ROOT/$PACKAGE")"
was="$(git -C "$ROOT" show "$base:$PACKAGE" 2>/dev/null | version_of 2>/dev/null || true)"
# Why: sort -V orders 0.10.0 after 0.9.0, which a plain comparison of strings gets wrong
higher() { [ "$1" != "$2" ] && [ "$(printf '%s\n%s\n' "$1" "$2" | sort -V | tail -n 1)" = "$1" ]; }

if oasdiff breaking "$TMP/base.json" "$ROOT/$CONTRACT" --fail-on ERR; then
    if [ -n "$was" ] && ! higher "$now" "$was"; then
        echo "api-diff.sh: the API contract changed since $base, but $PACKAGE still says $now. Raise its minor version (a compatible change), so a consumer can pin the new one." >&2
        exit 1
    fi
    echo "API contract changed since $base, compatibly (nothing breaks a consumer), as version $now"
else
    if [ -n "$was" ] && [ "${now%%.*}" -gt "${was%%.*}" ]; then
        echo "API contract breaks consumers since $base (see above), as the new major version $now: every consumer moves to it on purpose."
        exit 0
    fi
    echo "api-diff.sh: the API contract breaks consumers (see above). Keep the old shape and add the new one beside it, or, agreed with every consumer, raise the major version in $PACKAGE." >&2
    exit 1
fi
