#!/usr/bin/env bash
set -euo pipefail
if [ "${1:-}" = "--help" ]; then
    cat <<'HELP'
Says whether <version> is a release version of Suwageval.AgentSdk: the one rule, for both ways of publishing and the workflow.
Leading zeros are refused because nuget.org normalizes them away, so the package file would not carry the version asked for.
HELP
    exit 0
fi

if [[ $# -ne 1 ]]; then
  echo "usage: agent-sdk-version.sh <version>" >&2
  exit 2
fi
number='(0|[1-9][0-9]*)'
if ! [[ "$1" =~ ^$number\.$number\.$number(-[0-9A-Za-z.-]+)?$ ]]; then
  echo "version '$1' is not <major>.<minor>.<patch>[-prerelease]: pass it like 0.1.0 or 0.2.0-rc.1, without a leading v." >&2
  exit 1
fi
