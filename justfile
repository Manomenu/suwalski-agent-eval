set shell := ["bash", "-euo", "pipefail", "-c"]

suwgate := "Suwageval.Web/node_modules/.bin/suwgate"

# A copy of the local .env files in Bitwarden — `just secrets backup|restore` (secrets.just)
mod secrets

# The local stack in one tmux session: server, web, the example agent suwexag, a spare shell and a CLI shell — `just tmux-dev start [--restart]|stop` (tmux-dev.just)
mod tmux-dev

[private]
default:
    @just --list --unsorted

# HTTP API on :6230 (OpenAPI document at /openapi/v1.json), restarted on every change — needs `just db up`
[group('run')]
server *args:
    @./scripts/.internal/free-port.sh 6230
    @{{ suwgate }} dev server {{ args }}

# Web UI on :3230 — proxies /api to the server, so run `just server` alongside
[group('run')]
web *args:
    @./scripts/.internal/free-port.sh 3230
    cd Suwageval.Web && { [ -d node_modules ] || pnpm install; } && pnpm dev {{ args }}

# The local PostgreSQL on 127.0.0.1:5463 (compose profile db; data survives `down`): up, down, reset, psql, logs, status
[group('run')]
db *args:
    @{{ suwgate }} dev db {{ args }}

# Regenerate the API contract (contract/openapi.json) and the web app's types from it, after changing a record the API sends or receives; then raise the contract's version in contract/package.json
[group('dev')]
api-types:
    @./scripts/.internal/api-types.sh

# Run the suwageval CLI from source, e.g. `just cli validate ../my-agent/suwageval`
[group('dev')]
cli *args:
    dotnet run --project Suwageval.Cli -- {{ args }}

# Pack the CLI and install (or update) it as the global `suwageval` command
[group('dev')]
cli-install:
    dotnet pack Suwageval.Cli -c Release -o .artifacts/nupkg
    dotnet tool uninstall --global Suwageval.Cli || true
    dotnet tool install --global --add-source .artifacts/nupkg Suwageval.Cli

# Pack the agent contracts for a .NET agent under test into .artifacts/nupkg
[group('dev')]
agent-pack:
    dotnet pack Suwageval.AgentSdk -c Release -o .artifacts/nupkg

# Release Suwageval.AgentSdk as <version>: runs the agent-sdk workflow on master, which pushes to nuget.org (docs/release.md)
[group('dev')]
agent-publish version:
    @./scripts/.internal/agent-publish.sh {{ version }}

# The fallback: pack and push from this machine with a key from .env, for a first release the workflow cannot make or another feed (docs/release.md)
[group('dev')]
agent-publish-local version:
    @./scripts/.internal/agent-publish-local.sh {{ version }}

# Build and start the stack on http://localhost:8093
[group('infra')]
up:
    docker compose up -d --build
    @echo "web   http://localhost:8093"

# Build the images (suwageval-server:local, suwageval-web:local) another repository's stack runs, without starting them
[group('infra')]
build-images:
    docker compose build

# Rebuild the images without the layer cache, then start (slow, on purpose)
[group('infra')]
rebuild:
    docker compose build --no-cache
    @just up

# Stop the stack (images and the database volume stay)
[group('infra')]
down:
    docker compose down --remove-orphans

# What is running, on which ports, and where to open it
[group('infra')]
status:
    @./scripts/.internal/infra-status.sh

# Follow the logs of one service, or of all of them
[group('infra')]
logs *service:
    docker compose logs -f {{ service }}

# Browser tests of the whole app against a real server and database (also run by CI); not part of `just check`
[group('maintenance')]
e2e *args:
    @./scripts/.internal/e2e.sh {{ args }}

# The whole gate (suwgate check: build with analyzers, csharpier, every test, the web checks, the API contract and its version, acceptance criteria, the chart, compose, shellcheck, gitleaks) — the same CI runs; a worktree gets its own test database
[group('maintenance')]
check:
    @eval "$({{ suwgate }} dev checkout-env)" && {{ suwgate }} check

# The quick gate while working: the repository guards, and build, csharpier, the guard tests, eslint, tsc, vitest and shellcheck for what changed (what the Claude Code Stop hook runs)
[group('maintenance')]
check-quick:
    @{{ suwgate }} check --quick

# Apply autofixes and formatting: analyzer fixes and csharpier for C#, eslint and prettier for the web app
[group('maintenance')]
fmt:
    dotnet format style Suwageval.slnx --verbosity quiet && dotnet format analyzers Suwageval.slnx --verbosity quiet
    dotnet tool restore >/dev/null && dotnet csharpier format .
    cd Suwageval.Web && pnpm exec eslint --fix . && pnpm exec prettier --write --log-level warn .

# Install/refresh .NET and web dependencies from the lockfiles, and enable the git hooks
[group('maintenance')]
sync:
    git config core.hooksPath .githooks
    dotnet tool restore
    dotnet restore Suwageval.slnx
    @command -v pnpm >/dev/null || { echo "pnpm not found on PATH (just runs recipes in a non-interactive shell): install it, e.g. corepack enable --install-directory ~/.local/bin — see docs/development.md" >&2; exit 1; }
    cd Suwageval.Web && pnpm install --frozen-lockfile

# Only the live tests, which call the real model of the judge and the simulated user (from .env); the gate leaves them out (they skip with no model configured)
[group('maintenance')]
[positional-arguments]
live-check *args:
    @./scripts/.internal/live-check.sh "$@"
