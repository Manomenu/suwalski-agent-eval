#!/usr/bin/env bash
set -euo pipefail
if [ "${1:-}" = "--help" ]; then
    cat <<'HELP'
What of the container stack is running, on which ports, and where to open it. Behind `just status`.
HELP
    exit 0
fi
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

cd "$ROOT"

if ! command -v jq >/dev/null; then
    echo "jq is required by this script" >&2
    exit 1
fi

SERVICES="$(docker compose config --services 2>/dev/null | sort)"
if [ -z "$SERVICES" ]; then
    echo "no services defined in compose.yaml" >&2
    exit 1
fi

RUNNING="$(docker compose ps --format json 2>/dev/null \
    | jq -s 'if length == 1 and (.[0] | type) == "array" then .[0] else . end')"

printf '%-10s %-20s %-20s %s\n' "SERVICE" "STATE" "PORTS" "OPEN"

UP=0
for service in $SERVICES; do
    entry="$(printf '%s' "$RUNNING" | jq -c --arg s "$service" 'map(select(.Service == $s)) | first // empty')"

    if [ -z "$entry" ]; then
        printf '%-10s %-20s %-20s %s\n' "$service" "down" "-" "-"
        continue
    fi

    UP=$((UP + 1))

    state="$(printf '%s' "$entry" | jq -r '.State')"
    health="$(printf '%s' "$entry" | jq -r '.Health // ""')"
    [ -n "$health" ] && state="$state ($health)"

    ports="$(printf '%s' "$entry" | jq -r '
        (.Publishers // [])
        | map(if .PublishedPort > 0 then "\(.PublishedPort)->\(.TargetPort)" else "\(.TargetPort) internal" end)
        | join(", ")')"
    [ -z "$ports" ] && ports="-"

    url="$(printf '%s' "$entry" | jq -r '
        (.Publishers // [])
        | map(select(.PublishedPort > 0))
        | first
        | if . == null then "-" else "http://localhost:\(.PublishedPort)" end')"

    printf '%-10s %-20s %-20s %s\n' "$service" "$state" "$ports" "$url"
done

if [ "$UP" -eq 0 ]; then
    echo
    echo "nothing is up — just up"
fi
