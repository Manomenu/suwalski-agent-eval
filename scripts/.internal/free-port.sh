#!/usr/bin/env bash
# `just server` / `just web`: when something already listens on the port, show it and offer to stop it.
# Stops the listener's whole process group (e.g. `dotnet watch` with its server, `pnpm` with Vite), so a watcher
# does not start it again. Without a terminal to ask on, it only reports and fails.
set -euo pipefail

port="$1"
pid=$(ss -ltnpH "sport = :$port" 2>/dev/null | grep -oP 'pid=\K[0-9]+' | head -1 || true)
[[ -n "$pid" ]] || exit 0

pgid=$(ps -o pgid= -p "$pid" | tr -d ' ')
echo "Port $port is already in use by:" >&2
ps -o pid=,etime=,args= -g "$pgid" | sed 's/^/  /' >&2

if [[ ! -t 0 ]]; then
  echo "Stop it first (kill -- -$pgid)." >&2
  exit 1
fi
read -r -p "Stop it? [Y/n] " answer
if [[ -n "$answer" && ! "$answer" =~ ^[Yy] ]]; then
  exit 1
fi

kill -- "-$pgid"
for _ in {1..50}; do
  ss -ltnH "sport = :$port" | grep -q . || exit 0
  sleep 0.1
done
echo "Port $port is still in use after stopping process group $pgid." >&2
exit 1
