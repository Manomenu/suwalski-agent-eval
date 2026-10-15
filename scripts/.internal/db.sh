#!/usr/bin/env bash
# Local PostgreSQL for development and tests: one container whose data lives in a named
# volume, so it survives `down` and only `reset` wipes it. Humans call this through
# `just db <command>`; check.sh calls `up` before the tests. This repo works with docker
# (on Fedora: podman behind the docker command, AGENTS.md section 2).
set -euo pipefail

ENGINE=docker

NAME=agenteval-postgres
VOLUME=agenteval-postgres
# The major version the cluster runs (CloudNativePG); the minor is pinned like every image here.
IMAGE=docker.io/library/postgres:17.11
PORT=5463
USER=agenteval
DB=agenteval

wait_ready() {
    for _ in $(seq 1 30); do
        # Over TCP: on a fresh volume the image first runs a temporary, socket-only server for
        # its init scripts, and a socket check would call that one ready.
        "$ENGINE" exec "$NAME" pg_isready -q -h 127.0.0.1 -U "$USER" -d "$DB" && return 0
        sleep 1
    done
    echo "postgres did not become ready in 30 s — see: just db logs" >&2
    return 1
}

case "${1:-}" in
    up)
        if "$ENGINE" container inspect "$NAME" >/dev/null 2>&1; then
            # A container keeps the image it was created from; after a change of IMAGE the old
            # one would start silently (e.g. without an extension) and fail the migrations instead.
            # IDs, not names: the engine may store a digest reference in its own spelling. An IMAGE not
            # pulled yet has no ID here, which counts as a change too.
            if [ "$("$ENGINE" container inspect --format '{{.Image}}' "$NAME")" != "$("$ENGINE" image inspect --format '{{.Id}}' "$IMAGE" 2>/dev/null)" ]; then
                echo "$NAME runs another image than $IMAGE — \`just db reset\`, then \`just db up\`" >&2
                exit 1
            fi
            "$ENGINE" start "$NAME" >/dev/null
        else
            # Published on the loopback only: the password is a development one.
            "$ENGINE" run -d --name "$NAME" \
                -p "127.0.0.1:$PORT:5432" \
                -v "$VOLUME:/var/lib/postgresql/data" \
                -e POSTGRES_USER="$USER" -e POSTGRES_PASSWORD="$USER" -e POSTGRES_DB="$DB" \
                "$IMAGE" >/dev/null
        fi
        wait_ready
        echo "postgresql://$USER:$USER@localhost:$PORT/$DB"
        ;;
    down)
        "$ENGINE" stop "$NAME" >/dev/null && echo "stopped (data kept in volume $VOLUME)"
        ;;
    reset)
        read -r -p "Delete the local database with all its data? [y/N] " answer
        [ "$answer" = "y" ] || { echo "nothing deleted"; exit 1; }
        "$ENGINE" rm -f "$NAME" >/dev/null 2>&1 || true
        "$ENGINE" volume rm "$VOLUME" >/dev/null 2>&1 || true
        echo "deleted; \`just db up\` starts an empty one"
        ;;
    psql)
        shift
        # A terminal only when there is one, so `just db psql -c …` also works from scripts.
        tty=(); [ -t 0 ] && tty=(-t)
        "$ENGINE" exec -i "${tty[@]}" "$NAME" psql -U "$USER" -d "$DB" "$@"
        ;;
    logs)
        "$ENGINE" logs -f "$NAME"
        ;;
    status)
        if "$ENGINE" container inspect "$NAME" >/dev/null 2>&1; then
            "$ENGINE" ps -a --filter "name=^$NAME\$" --format "{{.Names}}  {{.Status}}  {{.Ports}}"
        else
            echo "no local database — \`just db up\` creates it"
        fi
        ;;
    *)
        echo "usage: $0 up|down|reset|psql|logs|status" >&2
        exit 2
        ;;
esac
