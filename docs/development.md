# Development

Working on Suwageval itself. The rules every change follows are in [AGENTS.md](../AGENTS.md); to only use
Suwageval, the [README](../README.md) is enough.

## Requirements

.NET SDK 10, `pnpm` (via corepack), `just`, `docker` with `docker compose` (on Fedora:
podman behind the `docker` command, with the Compose v2 plugin); for the full gate also
`helm`, `shellcheck` and `gitleaks`. After cloning: `just sync` (dependencies, the C#
formatter and the git hooks).

On Fedora (the owner's development machine):

```sh
sudo dnf install dotnet-sdk-10.0 nodejs nodejs-corepack podman podman-docker just helm ShellCheck
# docker compose: the official Compose v2 plugin (not podman-compose), talking to podman's socket
corepack enable --install-directory ~/.local/bin   # pnpm, in the version package.json pins
# gitleaks: the binary from https://github.com/gitleaks/gitleaks/releases, the version CI pins (8.30.1)
```

## Everyday commands

```sh
just                 # every recipe, grouped
cp .env.example .env # once: the model Suwageval calls (Models__fallback__*: address and name), never committed
just db up           # local PostgreSQL on :5463
just server          # API on :6230 (OpenAPI document at /openapi/v1.json)
just web             # web app on :3230, proxies /api to the server
just up              # the whole stack in containers on :8093 — no cluster needed
just build-images    # only build suwageval-server:local and suwageval-web:local, for another repository's compose stack
just check           # the quality gate, exactly what CI runs
just cli validate DIR  # the suwageval CLI from source; `just cli-install` installs it globally
just tmux-dev start  # tmux layout: server, web, the example agent suwexag (from source with `dotnet watch`, else its `suwexag:local` image), spare shell, CLI shell (SUWAGEVAL_AGENT_DIR sets the agent folder)
just tmux-dev start --restart  # the same, after killing the old session and everything running in it
just tmux-dev stop   # kill the session and everything running in it
just e2e             # browser tests against a real server and database
just secrets backup  # copy the local .env files into Bitwarden (restore on a new machine)
```

How the repo is organised and what every change must bring along: [AGENTS.md](../AGENTS.md).

