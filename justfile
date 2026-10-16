# Entry point for humans: `just` lists every recipe, grouped. Logic longer than a line or
# two lives in scripts/.internal/, which is also what CI and agents call directly.

set shell := ["bash", "-euo", "pipefail", "-c"]

# The local PostgreSQL — `just db` lists its recipes (.just/db.just)
mod db '.just/db.just'

# A copy of the local .env files in Bitwarden — `just secrets backup|restore` (.just/secrets.just)
mod secrets '.just/secrets.just'

[private]
default:
    @just --list --unsorted

# ---------------------------------------------------------------------------------------
# run — development servers on the host
# ---------------------------------------------------------------------------------------

# HTTP API on :6230 (OpenAPI document at /openapi/v1.json), rebuilt on every change — needs `just db up`
[group('run')]
server *args:
    DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1 dotnet watch --project Suwageval.Server run {{ args }}

# Web UI on :3230 — proxies /api to the server, so run `just server` alongside
[group('run')]
web *args:
    cd Suwageval.Web && { [ -d node_modules ] || pnpm install; } && pnpm dev {{ args }}

# ---------------------------------------------------------------------------------------
# dev — code generation and other chores while developing
# ---------------------------------------------------------------------------------------

# Regenerate the web app's TypeScript types from the server's API (after changing a record the API sends or receives)
[group('dev')]
api-types:
    @./scripts/.internal/api-types.sh

# Run the suwageval CLI from source, e.g. `just cli validate ../my-agent/suwageval`
[group('dev')]
cli *args:
    dotnet run --project Suwageval.Cli -- {{ args }}

# Open a tmux layout with the local stack: server, web, example agent, spare shell and a CLI shell (--restart: kill the old session first)
[group('dev')]
tmux-dev *args:
    @./scripts/.internal/tmux-dev.sh {{ args }}

# Pack the CLI and install (or update) it as the global `suwageval` command
[group('dev')]
cli-install:
    dotnet pack Suwageval.Cli -c Release -o .artifacts/nupkg
    dotnet tool uninstall --global Suwageval.Cli || true
    dotnet tool install --global --add-source .artifacts/nupkg Suwageval.Cli

# ---------------------------------------------------------------------------------------
# infra — the containerised stack: the same images the cluster runs, wired the same way
# ---------------------------------------------------------------------------------------

# Build and start the stack on http://localhost:8093
[group('infra')]
up:
    docker compose up -d --build
    @echo "web   http://localhost:8093"

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
logs service="":
    docker compose logs -f {{ service }}

# ---------------------------------------------------------------------------------------
# maintenance — quality gate and housekeeping
# ---------------------------------------------------------------------------------------

# Browser tests of the whole app against a real server and database (also run by CI)
[group('maintenance')]
e2e *args:
    @./scripts/.internal/e2e.sh {{ args }}

# Lint, types, tests, import contracts, dead code, the chart and compose — the same gate CI runs
[group('maintenance')]
check:
    @./scripts/.internal/check.sh

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
    cd Suwageval.Web && pnpm install --frozen-lockfile
