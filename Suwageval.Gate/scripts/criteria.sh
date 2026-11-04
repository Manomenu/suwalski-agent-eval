#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SELF="$ROOT/Suwageval.Gate/scripts/criteria.sh"
REPO="Manomenu/suwalski-agent-plugin"
TOOL_COMMIT=02a5f3d98c5caa9f69af3fa0508efa566a6d78f2
TOOL_SHA256=d83e3a4e7c7f31bbe28d7eacc8eb5dbab8ba4298601839e827638ce716a91088
TOOL="${XDG_CACHE_HOME:-$HOME/.cache}/suwagent/vdoc-$TOOL_COMMIT.py"

url() { echo "https://raw.githubusercontent.com/$REPO/$1/plugins/suwagent/scripts/vdoc.py"; }

fetch() {
    if [ ! -f "$TOOL" ]; then
        mkdir -p "$(dirname "$TOOL")"
        curl -sSfL -o "$TOOL.part" "$(url "$TOOL_COMMIT")" || {
            echo "criteria.sh: cannot download vdoc.py at $TOOL_COMMIT from $(url "$TOOL_COMMIT"): no network? Run it again once online." >&2
            rm -f "$TOOL.part"
            exit 3
        }
        mv "$TOOL.part" "$TOOL"
    fi
    if ! echo "$TOOL_SHA256  $TOOL" | sha256sum --check --status; then
        echo "criteria.sh: $TOOL does not match TOOL_SHA256 in $SELF: delete it and run again; if it still differs, the pin is wrong: run just criteria-update." >&2
        exit 4
    fi
}

case "${1:-}" in
    --help)
        cat <<'HELP'
Which guardrails keep each acceptance criterion of the specs true? Runs `vdoc.py criteria` of the suwagent plugin,
pinned by commit and SHA-256, so the answer is the same on every machine and in CI. This project's kinds of
guardrail (name, path globs, how the id is written) are in criteria.json.

  criteria.sh                    every criterion: id, kind, file:line; exit 1, with the fix, on a problem
  criteria.sh AC-<feature>-<n>   one criterion
  criteria.sh --fetch            only download the pinned tool into the cache (exit 3: it cannot be downloaded)
  criteria.sh --update           move the pin to the plugin's newest master (`just criteria-update`)
HELP
        ;;
    --fetch)
        fetch
        ;;
    --update)
        commit="$(git ls-remote "https://github.com/$REPO.git" refs/heads/master | cut -f1)"
        part="$(mktemp)"
        curl -sSfL -o "$part" "$(url "$commit")"
        sha="$(sha256sum "$part" | cut -d' ' -f1)"
        rm -f "$part"
        sed -i "s/^TOOL_COMMIT=.*/TOOL_COMMIT=$commit/; s/^TOOL_SHA256=.*/TOOL_SHA256=$sha/" "$SELF"
        echo "criteria.sh now pins vdoc.py at $commit (sha256 $sha); run the full gate, then commit the change."
        ;;
    *)
        fetch
        exec python3 "$TOOL" criteria --root "$ROOT" --config Suwageval.Gate/criteria.json "$@"
        ;;
esac
