#!/usr/bin/env bash
# Why: Claude Code Stop hook: a red quick gate blocks the stop once (exit 2), then lets go, so a gate the agent cannot fix never loops
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

input="$(cat)"
if printf '%s' "$input" | grep -Eq '"stop_hook_active"[[:space:]]*:[[:space:]]*true'; then
    exit 0
fi

# Why: a clean `git status` is not enough, because a change committed in the same turn leaves it clean and unchecked
fingerprint() {
    {
        git -C "$ROOT" rev-parse HEAD 2>/dev/null || true
        git -C "$ROOT" diff HEAD --binary 2>/dev/null || git -C "$ROOT" diff --cached --binary
        git -C "$ROOT" ls-files --others --exclude-standard -z | (cd "$ROOT" && xargs -0 -r sha256sum)
    } | sha256sum | cut -d' ' -f1
}
green="$(git -C "$ROOT" rev-parse --git-path quick-gate-green)"
case "$green" in /*) ;; *) green="$ROOT/$green" ;; esac
now="$(fingerprint)"
if [ -f "$green" ] && [ "$(cat "$green")" = "$now" ]; then
    exit 0
fi

if output="$(bash "$ROOT/Suwageval.Gate/scripts/check.sh" --quick 2>&1)"; then
    printf '%s\n' "$now" >"$green"
    exit 0
fi
{
    echo "The quick gate (Suwageval.Gate/scripts/check.sh --quick) is red. Fix it before finishing:"
    printf '%s\n' "$output" | tail -n 60
} >&2
exit 2
