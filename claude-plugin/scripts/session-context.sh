#!/bin/sh
# Why: a SessionStart hook: Claude Code adds what this prints to the context, so outside an agent's repository it prints nothing
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
