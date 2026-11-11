#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'HELP'
The quality gate. CI runs exactly this script; humans run `just check`, the Stop hook runs --quick.

  check.sh --quick   the repository guards, with build, csharpier, the CLI's tests, tsc, vitest, eslint and shellcheck only for
                     what changed; no database
  check.sh           build, csharpier and every test (server against the real database, CLI, guards, vitest), plus tsc, eslint,
                     prettier, knip, comments of the web app, API contract and types up to date, the contract's version right
                     for what changed since master (api-diff.sh, oasdiff), acceptance criteria guarded, the chart, compose,
                     shellcheck and gitleaks

What changed is what differs from where this branch left master, committed or not, plus new untracked files. With
no .NET file changed, quick builds and tests only Suwageval.Gate; the web steps run only when web code changed.

A green gate prints one line. Otherwise it prints the tail of each failed step's log and lists the failed and
skipped steps. A step that cannot run because a tool is missing is SKIP, never PASS: with CI set a SKIP fails the
gate, locally the report ends with NOT VERIFIED: <steps>. Every step has a ceiling (CHECK_STEP_LIMIT overrides it);
a step above it is killed and fails as hung. The quick gate has a time budget, CHECK_QUICK_LIMIT seconds (default 60):
the Stop hook runs it after every turn of an agent, and a gate that takes minutes gets switched off. Above the budget
it fails and says so: move the slow step to the full gate or speed it up; raise the budget only on a machine that is
slow across the board. Acceptance criteria are not in --quick: a criterion written into a spec before its guardrail,
or one the owner must first decide on, would block the agent from stopping to ask.
Tests marked [Trait("live", "true")] call a real model and are left out: scripts/.internal/live-check.sh runs them.
HELP
}

QUICK=0
case "${1:-}" in
    "") ;;
    --quick) QUICK=1 ;;
    --help | -h)
        usage
        exit 0
        ;;
    *)
        usage >&2
        exit 2
        ;;
esac

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
# shellcheck source=scripts/.internal/checkout-env.sh
source "$ROOT/scripts/.internal/checkout-env.sh"

QUICK_LIMIT="${CHECK_QUICK_LIMIT:-60}"
case "$QUICK_LIMIT" in
    '' | *[!0-9]*)
        echo "check.sh: CHECK_QUICK_LIMIT must be a whole number of seconds, not '$QUICK_LIMIT'" >&2
        exit 2
        ;;
esac

LOGS="$(mktemp -d)"
trap 'rm -rf "$LOGS"' EXIT
FAILURE_LINES=25
ORDER=()
declare -A NAME
declare -A SKIPPED
declare -A LIMIT

declare_step() { ORDER+=("$1"); NAME["$1"]="$2"; LIMIT["$1"]="$3"; }

run_step() {
    local key="$1" function="$2" start status=0
    start=$SECONDS
    if command -v timeout >/dev/null 2>&1; then
        timeout -k 10 "${CHECK_STEP_LIMIT:-${LIMIT[$key]}}" bash -c "$function" >"$LOGS/$key.log" 2>&1 || status=$?
        if [ "$status" -eq 124 ]; then
            echo "hung or too slow: $function exceeded ${CHECK_STEP_LIMIT:-${LIMIT[$key]}} s and was killed" >>"$LOGS/$key.log"
        fi
    else
        bash -c "$function" >"$LOGS/$key.log" 2>&1 || status=$?
    fi
    echo "$status $((SECONDS - start))" >"$LOGS/$key.result"
}

build_server() { cd "$ROOT" && dotnet build Suwageval.slnx --nologo -v quiet; }

format_server() { cd "$ROOT" && dotnet tool restore >/dev/null && dotnet csharpier check .; }

local_database() { "$ROOT/scripts/.internal/db.sh" up >/dev/null; }

# Why: a quick check must cost what the change costs, not what the repository has grown to
changed_files() {
    cd "$ROOT"
    local base
    # Why: without master (a fresh clone of one branch) everything tracked counts as changed
    base="$(git merge-base HEAD master 2>/dev/null || git merge-base HEAD origin/master 2>/dev/null || true)"
    {
        if [ -n "$base" ]; then git diff --name-only --diff-filter=d "$base"; else git ls-files --cached; fi
        git ls-files --others --exclude-standard
    } | sort -u
}

dotnet_changed() {
    changed_files |
        grep -qE '\.(cs|csproj|props|targets|slnx|rsp)$|(^|/)(\.editorconfig|BannedSymbols\.txt|global\.json|packages\.lock\.json|appsettings[^/]*\.json)$'
}

build_changed() {
    cd "$ROOT"
    if dotnet_changed; then
        dotnet build Suwageval.slnx --nologo -v quiet
    else
        dotnet build Suwageval.Gate/Suwageval.Gate.csproj --nologo -v quiet
    fi
}

format_changed() {
    cd "$ROOT"
    local files
    mapfile -t files < <(changed_files | grep -E '\.cs$' || true)
    if [ "${#files[@]}" -gt 0 ]; then
        dotnet tool restore >/dev/null && dotnet csharpier check "${files[@]}"
    fi
}

# Why: --no-build runs what the build step just produced; no database, the server's tests are left to the full gate
test_guards() {
    cd "$ROOT" && dotnet test --project Suwageval.Gate --no-build --no-progress --filter-not-trait "live=true" || return
    if dotnet_changed; then dotnet test --project Suwageval.Cli.Tests --no-build --no-progress --filter-not-trait "live=true"; fi
}

web_changed_files() { changed_files | grep -E '^Suwageval\.Web/.*\.(ts|tsx|js|jsx|mjs|cjs)$' | sed 's#^Suwageval\.Web/##' || true; }

typecheck_changed() { if [ -n "$(web_changed_files)" ]; then web_typecheck; fi; }

vitest_changed() { if [ -n "$(web_changed_files)" ]; then web_vitest; fi; }

eslint_changed() {
    local files
    mapfile -t files < <(web_changed_files)
    if [ "${#files[@]}" -gt 0 ]; then
        cd "$ROOT/Suwageval.Web" && pnpm exec eslint --no-warn-ignored --max-warnings 0 "${files[@]}"
    fi
}

shell_changed() {
    cd "$ROOT"
    local files
    mapfile -t files < <(changed_files | grep -E '\.sh$|^\.githooks/pre-commit$' || true)
    if [ "${#files[@]}" -gt 0 ]; then
        shellcheck -S warning "${files[@]}"
    fi
}

test_server() { cd "$ROOT" && dotnet test --solution Suwageval.slnx --no-build --no-progress --filter-not-trait "live=true"; }

web_typecheck() { cd "$ROOT/Suwageval.Web" && pnpm exec tsc --noEmit -p tsconfig.app.json && pnpm exec tsc --noEmit -p tsconfig.node.json; }

web_eslint() { cd "$ROOT/Suwageval.Web" && pnpm exec eslint . --max-warnings 0; }

web_vitest() { cd "$ROOT/Suwageval.Web" && pnpm exec vitest run; }

web_prettier() { cd "$ROOT/Suwageval.Web" && pnpm exec prettier --check .; }

web_knip() { cd "$ROOT/Suwageval.Web" && pnpm exec knip; }

web_comments() { node "$ROOT/Suwageval.Gate/scripts/web-comments.mjs"; }

api_types() { "$ROOT/scripts/.internal/api-types.sh" --check; }

api_version() { "$ROOT/scripts/.internal/api-diff.sh"; }

acceptance_criteria() { "$ROOT/Suwageval.Gate/scripts/criteria.sh"; }

helm_chart() {
    cd "$ROOT"
    helm lint deploy/chart --set image.tag=sha-lint --quiet
    # Why: an annotation or label name above 63 characters after the slash lints and renders, and only the API server refuses it, at deployment
    helm template lint-check deploy/chart --set image.tag=sha-lint |
        grep -oE '^[[:space:]]+[a-z0-9.-]+\.[a-z]+/[A-Za-z0-9._-]+:' | sed -E 's/^[[:space:]]+//; s/:$//' |
        awk -F/ 'length($2) > 63 { print "  name too long (" length($2) " > 63 after the slash): " $0; bad = 1 } END { exit bad }'
}

compose_file() { cd "$ROOT" && docker compose config --quiet; }

shell_scripts() {
    cd "$ROOT"
    # Why: warnings and up, because the info level flags every function run_step calls by name
    { find scripts claude-plugin/scripts Suwageval.Gate/scripts -name "*.sh"; echo .githooks/pre-commit; } | xargs shellcheck -S warning
}

untracked_files_have_no_secrets() {
    local file found=0
    while IFS= read -r -d '' file; do
        gitleaks dir "$file" --no-banner --redact --log-level warn || found=1
    done < <(git ls-files --others --exclude-standard -z)
    return "$found"
}

secrets_in_git() {
    cd "$ROOT"
    # Why: a secret deleted in a later commit is still published, and `git --pre-commit` sees only changes to tracked files
    gitleaks git . --no-banner --redact --log-level warn &&
        gitleaks git . --pre-commit --no-banner --redact --log-level warn &&
        untracked_files_have_no_secrets
}

export ROOT
export -f build_server format_server local_database test_server web_typecheck web_eslint web_vitest web_prettier web_knip web_comments \
    api_types api_version acceptance_criteria helm_chart compose_file shell_scripts untracked_files_have_no_secrets secrets_in_git
export -f changed_files dotnet_changed build_changed format_changed test_guards web_changed_files typecheck_changed vitest_changed \
    eslint_changed shell_changed

if [ "$QUICK" -eq 1 ]; then
    declare_step build "build what changed (compiler, nullable, analyzers)" 420
    declare_step format "format changed C# (csharpier check)" 180
    declare_step test "repository guards, and the CLI's tests when .NET changed (xunit, no database)" 300
    declare_step tsc "typecheck Suwageval.Web if it changed (tsc)" 300
    declare_step vitest "test Suwageval.Web if it changed (vitest)" 300
    declare_step eslint "lint changed web files (eslint)" 420
    declare_step shellcheck "changed shell scripts (shellcheck)" 60
    command -v shellcheck >/dev/null 2>&1 || SKIPPED[shellcheck]="shellcheck not installed"
else
    declare_step build "build server (compiler, nullable, analyzers, NuGet audit)" 420
    declare_step format "format server (csharpier check)" 180
    if [ -z "${CI:-}" ]; then declare_step postgres "postgres for tests (db.sh up)" 120; fi
    declare_step test "test server (xunit, repository guards, real PostgreSQL)" 900
    declare_step tsc "typecheck Suwageval.Web (tsc: app, e2e tests, config)" 300
    declare_step vitest "test Suwageval.Web (vitest)" 300
    declare_step eslint "lint Suwageval.Web (eslint strict + import contracts, no warnings)" 420
    declare_step prettier "format Suwageval.Web (prettier --check)" 180
    declare_step knip "dead code Suwageval.Web (knip)" 240
    declare_step comments "comments of Suwageval.Web (web-comments.mjs)" 120
    declare_step apitypes "API contract and types up to date (api-types.sh --check)" 420
    declare_step apiversion "API contract's version right for what changed since master (api-diff.sh, oasdiff)" 60
    declare_step criteria "acceptance criteria guarded (criteria.sh)" 60
    declare_step helm "helm chart (lint + template)" 120
    declare_step shellcheck "shell scripts (shellcheck)" 120
    declare_step gitleaks "secrets in git (gitleaks)" 420
    declare_step compose "compose stack (compose config)" 120

    command -v helm >/dev/null 2>&1 || SKIPPED[helm]="helm not installed"
    command -v shellcheck >/dev/null 2>&1 || SKIPPED[shellcheck]="shellcheck not installed"
    command -v gitleaks >/dev/null 2>&1 || SKIPPED[gitleaks]="gitleaks not installed"
    docker compose version >/dev/null 2>&1 || SKIPPED[compose]="docker compose not installed"
    command -v oasdiff >/dev/null 2>&1 || SKIPPED[apiversion]="oasdiff not installed"
    if ! command -v python3 >/dev/null 2>&1; then
        SKIPPED[criteria]="python3 not installed"
    else
        fetched=0
        "$ROOT/Suwageval.Gate/scripts/criteria.sh" --fetch >/dev/null 2>&1 || fetched=$?
        # Why: only a missing download is a SKIP; a checksum mismatch runs the step, which fails and says why
        if [ "$fetched" -eq 3 ]; then SKIPPED[criteria]="the pinned vdoc.py cannot be downloaded (no network?)"; fi
    fi
fi

# Why: installed once, before the web steps start, so they do not install side by side
[ -d "$ROOT/Suwageval.Web/node_modules" ] || (cd "$ROOT/Suwageval.Web" && pnpm install --frozen-lockfile --silent)

GATE_START=$SECONDS
PIDS=()
# Why: sequential, because the tests run on what the build produced and the API types step builds the same project
(
    if [ "$QUICK" -eq 1 ]; then
        run_step build build_changed
        run_step test test_guards
    else
        run_step build build_server
        if [ -z "${CI:-}" ]; then run_step postgres local_database; fi
        run_step test test_server
        run_step apitypes api_types
        # Why: api-diff.sh compares the document the step above has just proven current
        if [ -z "${SKIPPED[apiversion]+x}" ]; then run_step apiversion api_version; fi
    fi
) &
PIDS+=($!)
launch() {
    local key="$1"
    if [ -z "${SKIPPED[$key]+x}" ]; then
        run_step "$key" "$2" &
        PIDS+=($!)
    fi
}
if [ "$QUICK" -eq 1 ]; then
    launch format format_changed
    launch tsc typecheck_changed
    launch vitest vitest_changed
    launch eslint eslint_changed
    launch shellcheck shell_changed
else
    launch format format_server
    launch tsc web_typecheck
    launch vitest web_vitest
    launch eslint web_eslint
    launch prettier web_prettier
    launch knip web_knip
    launch comments web_comments
    launch criteria acceptance_criteria
    launch helm helm_chart
    launch shellcheck shell_scripts
    launch gitleaks secrets_in_git
    launch compose compose_file
fi
for pid in "${PIDS[@]}"; do wait "$pid" || true; done
TOTAL=$((SECONDS - GATE_START))
# Why: the budget is decided before the summary, so a green run over budget never prints PASS
OVER_BUDGET=0
if [ "$QUICK" -eq 1 ] && [ "$TOTAL" -gt "$QUICK_LIMIT" ]; then OVER_BUDGET=1; fi

FAILED=0
PASSED=0
NOT_VERIFIED=()
REPORT=()
FAILURES=()
for key in "${ORDER[@]}"; do
    if [ -n "${SKIPPED[$key]+x}" ]; then
        if [ -n "${CI:-}" ]; then
            REPORT+=("FAIL        ${NAME[$key]} — ${SKIPPED[$key]} (CI: a skipped step fails)")
            FAILED=1
        else
            REPORT+=("SKIP        ${NAME[$key]} — ${SKIPPED[$key]}")
            NOT_VERIFIED+=("$key")
        fi
        continue
    fi
    read -r status secs <"$LOGS/$key.result"
    if [ "$status" -eq 0 ]; then
        PASSED=$((PASSED + 1))
    else
        REPORT+=("$(printf 'FAIL %4ss  %s' "$secs" "${NAME[$key]}")")
        FAILURES+=("${NAME[$key]}" "$LOGS/$key.log")
        FAILED=1
    fi
done
if [ "$OVER_BUDGET" -eq 1 ]; then
    REPORT+=("FAIL ${TOTAL}s  the quick gate is over its budget of ${QUICK_LIMIT}s: move the slow step to the full gate or speed it up; a first run after a reboot or on a fresh checkout may exceed it once; a machine that is slow across the board runs with CHECK_QUICK_LIMIT=<seconds>")
    FAILED=1
fi

for ((i = 0; i < ${#FAILURES[@]}; i += 2)); do
    echo
    echo "==================== failed: ${FAILURES[i]} (last $FAILURE_LINES lines) ===================="
    tail -n "$FAILURE_LINES" "${FAILURES[i + 1]}"
done

if [ ${#REPORT[@]} -eq 0 ]; then
    echo "PASS check.sh${1:+ $1}: all $PASSED steps in ${TOTAL}s"
else
    echo
    for line in "${REPORT[@]}"; do
        echo "  $line"
    done
    echo "  $PASSED passed, total ${TOTAL}s"
fi
if [ ${#NOT_VERIFIED[@]} -gt 0 ]; then
    echo "NOT VERIFIED: ${NOT_VERIFIED[*]}"
fi
exit "$FAILED"
