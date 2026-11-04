#!/usr/bin/env bash
# Why: .github/workflows/ci.yml still calls this path; delete this file in the change that points the workflow at Suwageval.Gate/scripts/check.sh
exec "$(cd "$(dirname "$0")/../.." && pwd)/Suwageval.Gate/scripts/check.sh" "$@"
