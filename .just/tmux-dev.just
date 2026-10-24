# `just tmux-dev <recipe>` — the local stack in one tmux session: server, web, the example agent suwexag, a spare shell
# and a CLI shell. Humans only; the logic is in scripts/.internal/tmux-dev.sh.

set shell := ["bash", "-euo", "pipefail", "-c"]
# Recipes run from the repo root, like the ones in the main justfile.
set working-directory := ".."

[private]
default:
    @just --list tmux-dev --unsorted

# Open the session, or attach to the one running (--restart: kill the old session and everything in it first)
start *args:
    @./scripts/.internal/tmux-dev.sh start {{ args }}

# Kill the session and everything running in it
stop:
    @./scripts/.internal/tmux-dev.sh stop
