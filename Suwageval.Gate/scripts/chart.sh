#!/usr/bin/env bash
set -euo pipefail
usage() {
    cat <<'USAGE'
The Helm chart (deploy/chart) lints and renders, and no annotation or label name is longer than 63 characters after
its slash: such a name lints and renders, and only the Kubernetes API server refuses it, at deployment. A step of
suwgate check (suwgate.yml); exit 1 on a problem, also when helm is not installed.
USAGE
}
if [ "${1:-}" = "--help" ]; then
    usage
    exit 0
fi
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

command -v helm >/dev/null || {
    echo "helm is not installed: install Helm 3 (https://helm.sh/docs/intro/install/), the chart cannot be checked without it" >&2
    exit 1
}
helm lint deploy/chart --set image.tag=sha-lint --quiet
helm template lint-check deploy/chart --set image.tag=sha-lint |
    grep -oE '^[[:space:]]+[a-z0-9.-]+\.[a-z]+/[A-Za-z0-9._-]+:' | sed -E 's/^[[:space:]]+//; s/:$//' |
    awk -F/ 'length($2) > 63 { print "  name too long (" length($2) " > 63 after the slash): " $0; bad = 1 } END { exit bad }'
