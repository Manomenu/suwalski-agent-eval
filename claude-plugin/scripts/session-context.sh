#!/bin/sh
if [ "${1:-}" = "--help" ]; then
  echo "The plugin's SessionStart hook (hooks/hooks.json): in an agent's repository, one with suwageval/agent.yaml under"
  echo "CLAUDE_PROJECT_DIR, it prints the Suwageval loop into the session's context; anywhere else it prints nothing."
  exit 0
fi
project="${CLAUDE_PROJECT_DIR:-$PWD}"
[ -f "$project/suwageval/agent.yaml" ] || exit 0
cat "$(dirname "$0")/../hooks/context/agent-loop.md"
if grep -Eq '^[[:space:]]+monitoring:' "$project/suwageval/agent.yaml"; then
  echo
  echo "Monitoring is on in this repository (features.monitoring): real conversations of the deployed agent are in Suwageval."
fi
command -v suwageval >/dev/null 2>&1 || [ -x "$HOME/.dotnet/tools/suwageval" ] || {
  echo
  echo "The suwageval CLI is not on PATH here: the suwageval skill says how to install it."
}
