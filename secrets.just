set shell := ["bash", "-euo", "pipefail", "-c"]
set positional-arguments

[private]
default:
    @just --list secrets --unsorted

# Copy .env and <project>/.env into Bitwarden (folder Homelab), after changing one
backup:
    @./scripts/.internal/secrets.sh backup

# New machine: Bitwarden → the .env files. --force overwrites files that differ
restore *args:
    @./scripts/.internal/secrets.sh restore "$@"
