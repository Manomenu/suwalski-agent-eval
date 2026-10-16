#!/usr/bin/env bash
# Lint, type-check and test the whole solution and print a summary report.
# Exit code is non-zero if any step failed. CI runs exactly this script; humans run `just check`.
# What each step guards, and what a new feature adds to it: AGENTS.md, "The quality gate".
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

declare -a REPORT
FAILED=0

run_step() {
    local name="$1"; shift
    echo
    echo "==> $name"
    if "$@"; then
        REPORT+=("PASS  $name")
    else
        REPORT+=("FAIL  $name")
        FAILED=1
    fi
}

build_server() {
    # Compiler, nullable reference types and every analyzer (Directory.Build.props, .editorconfig):
    # one warning fails it. Also NuGet's audit: a package with a known vulnerability is a warning too.
    (cd "$ROOT" && dotnet build Suwageval.slnx --nologo -v quiet)
}

format_server() {
    # Does not rewrite anything — `just fmt` does.
    (cd "$ROOT" && dotnet tool restore >/dev/null && dotnet csharpier check .)
}

test_server() {
    # Includes ContractsTests: the import contracts, and every server namespace has a layer.
    # Tests marked [Trait("live", "true")] call a real outside service (the judge's model): they are left
    # out here — the gate never calls a model — and run by scripts/.internal/live-check.sh.
    (cd "$ROOT" && dotnet test --solution Suwageval.slnx --no-build --no-progress --filter-not-trait "live=true")
}

web() {
    # Same order of concerns as the server: types, lint, format, tests.
    cd "$ROOT/Suwageval.Web"
    [ -d node_modules ] || pnpm install --frozen-lockfile --silent
    pnpm exec "$@"
}

helm_chart() {
    # `lint` catches syntax, but passes a template that never renders; `template` actually
    # renders it. The tag is made up: this checks the shape, not whether the image exists.
    cd "$ROOT"
    helm lint deploy/chart --set image.tag=sha-lint --quiet
    # Kubernetes allows 63 characters in an annotation or label name after the prefix's slash.
    # A longer one renders, lints and passes schemas, and is refused only by the API server —
    # at deployment time.
    helm template lint-check deploy/chart --set image.tag=sha-lint |
        grep -oE '^[[:space:]]+[a-z0-9.-]+\.[a-z]+/[A-Za-z0-9._-]+:' | sed -E 's/^[[:space:]]+//; s/:$//' |
        awk -F/ 'length($2) > 63 { print "  name too long (" length($2) " > 63 after the slash): " $0; bad = 1 } END { exit bad }'
}

compose_file() {
    # The local stack (compose.yaml) must at least parse and resolve; `just up` builds it.
    # docker compose on the laptop and on the CI runner alike (AGENTS.md section 2).
    (cd "$ROOT" && "${COMPOSE[@]}" config --quiet)
}

shell_scripts() {
    # Warnings and up: the info level flags every function run_step calls by name.
    (cd "$ROOT" && { find scripts -name "*.sh"; echo .githooks/pre-commit; } | xargs shellcheck -S warning)
}

secrets_in_git() {
    # The whole history (a secret deleted in a later commit is still published), then what is
    # changed but not committed yet. The pre-commit hook (.githooks/) checks each commit too.
    (cd "$ROOT" && gitleaks git . --no-banner --redact --log-level warn &&
        gitleaks git . --pre-commit --no-banner --redact --log-level warn &&
        untracked_files_have_no_secrets)
}

untracked_files_have_no_secrets() {
    # `git . --pre-commit` sees only changes to tracked files; a new file would first be caught
    # by the pre-commit hook, after the gate said PASS. Each new (not ignored) file is scanned.
    local file found=0
    while IFS= read -r -d '' file; do
        gitleaks dir "$file" --no-banner --redact --log-level warn || found=1
    done < <(git ls-files --others --exclude-standard -z)
    return "$found"
}

local_database() {
    # The tests need PostgreSQL. CI provides it as a service; here it is the container
    # `just db up` manages, started if it is not running yet.
    "$ROOT/scripts/.internal/db.sh" up >/dev/null
}

run_step "build server (compiler, nullable, analyzers, NuGet audit)" build_server
run_step "format server (csharpier check)" format_server
if [ -z "${CI:-}" ]; then
    run_step "postgres for tests (just db up)" local_database
fi
run_step "test server (xunit, incl. import contracts)" test_server
# Two TypeScript projects: the app (browser) and the code that runs in Node (vite config, e2e
# tests). This checks the types of both; it does not run the e2e tests (that is `just e2e`).
web_typecheck() { web tsc --noEmit -p tsconfig.app.json && web tsc --noEmit -p tsconfig.node.json; }
run_step "typecheck Suwageval.Web (tsc: app, e2e tests, config)" web_typecheck
run_step "lint Suwageval.Web (eslint strict + import contracts)" web eslint .
run_step "format Suwageval.Web (prettier --check)" web prettier --check .
run_step "test Suwageval.Web (vitest)" web vitest run
# Unused files, exports and dependencies. knip.json leaves out the generated API types and
# openapi-typescript, which only scripts/.internal/api-types.sh uses (knip reads no shell).
run_step "dead code Suwageval.Web (knip)" web knip
run_step "API types up to date (just api-types)" "$ROOT/scripts/.internal/api-types.sh" --check

# Tools a fresh machine may lack are skipped with a visible note instead of a PASS — a
# silently skipped step is worse than no step. CI installs helm, so there it always runs.
if command -v helm >/dev/null 2>&1; then
    run_step "helm chart (lint + template)" helm_chart
else
    REPORT+=("SKIP  helm chart — helm not installed")
fi
if command -v shellcheck >/dev/null 2>&1; then
    run_step "shell scripts (shellcheck)" shell_scripts
else
    REPORT+=("SKIP  shell scripts — shellcheck not installed")
fi
if command -v gitleaks >/dev/null 2>&1; then
    run_step "secrets in git (gitleaks)" secrets_in_git
else
    REPORT+=("SKIP  secrets in git — gitleaks not installed (CI always runs it)")
fi
COMPOSE=()
if command -v docker >/dev/null 2>&1 && docker compose version >/dev/null 2>&1; then
    COMPOSE=(docker compose)
fi
if [ ${#COMPOSE[@]} -gt 0 ]; then
    run_step "compose stack (compose config)" compose_file
else
    REPORT+=("SKIP  compose stack — docker compose not installed")
fi

echo
echo "==================== check report ===================="
for line in "${REPORT[@]}"; do
    echo "  $line"
done
echo "======================================================"
exit "$FAILED"
