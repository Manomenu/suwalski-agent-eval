#!/usr/bin/env bash
# `just tmux-dev`: the local stack in one tmux session.
#   left:  a shell in the example agent's repo, for the suwageval CLI
#   right: server (+ database) | web  /  example agent | a spare shell
# The session is reused when it exists; inside tmux the client is switched, not nested.
# TMUX_DEV_NO_ATTACH=1 builds the session and stops there (used to inspect the layout).
set -euo pipefail

SESSION=suwageval-dev
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
AGENT_DIR="${SUWAGEVAL_AGENT_DIR:-$HOME/repos/suwalski-example-agent}"

if ! command -v tmux >/dev/null 2>&1; then
  echo "tmux is not installed. Install it: sudo dnf install tmux" >&2
  exit 1
fi

attach() {
  if [[ "${TMUX_DEV_NO_ATTACH:-}" == "1" ]]; then
    return
  fi
  if [[ -n "${TMUX:-}" ]]; then
    tmux switch-client -t "$SESSION"
  else
    tmux attach-session -t "$SESSION"
  fi
}

if tmux has-session -t "=$SESSION" 2>/dev/null; then
  attach
  exit 0
fi

# Types a command into a pane's shell, so the pane survives the command's exit.
run() { tmux send-keys -t "$1" "$2" Enter; }

agent_present=1
[[ -d "$AGENT_DIR" ]] || agent_present=0
left_dir="$AGENT_DIR"
((agent_present)) || left_dir="$REPO"

left=$(tmux new-session -d -s "$SESSION" -c "$left_dir" -x 220 -y 50 -P -F '#{pane_id}')
top_left=$(tmux split-window -h -t "$left" -c "$REPO" -P -F '#{pane_id}')
bottom_left=$(tmux split-window -v -t "$top_left" -c "$REPO" -P -F '#{pane_id}')
top_right=$(tmux split-window -h -t "$top_left" -c "$REPO" -P -F '#{pane_id}')
bottom_right=$(tmux split-window -h -t "$bottom_left" -c "$REPO" -P -F '#{pane_id}')

tmux set-option -t "$SESSION" pane-border-status top
tmux select-pane -t "$left" -T "cli"
tmux select-pane -t "$top_left" -T "server"
tmux select-pane -t "$top_right" -T "web"
tmux select-pane -t "$bottom_left" -T "example agent"
tmux select-pane -t "$bottom_right" -T "shell"

run "$top_left" "just db up && just server"
run "$top_right" "just web"

if ((agent_present)); then
  run "$bottom_left" "cd $(printf '%q' "$AGENT_DIR") && uv run uvicorn example_agent.app:app --port 7010"
else
  run "$bottom_left" "echo $(printf '%q' "Example agent not found: $AGENT_DIR (set SUWAGEVAL_AGENT_DIR)")"
  run "$left" "echo $(printf '%q' "Example agent not found: $AGENT_DIR (set SUWAGEVAL_AGENT_DIR); this shell is in the suwageval repo")"
fi

tmux select-pane -t "$left"
attach
