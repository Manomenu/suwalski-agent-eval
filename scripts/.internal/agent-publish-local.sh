#!/usr/bin/env bash
set -euo pipefail
if [ "${1:-}" = "--help" ]; then
    cat <<'HELP'
The fallback to `just agent-publish`, which is the normal way, for the two things the workflow cannot do:
a first release nuget.org's Trusted Publishing refuses, and another feed. Packs Suwageval.AgentSdk as <version> on this machine and
pushes it: to nuget.org with AGENT_SDK_NUGET_API_KEY, or, when AGENT_SDK_FEED_URL is set, to that feed with AGENT_SDK_FEED_API_KEY.
The values come from the gitignored .env.
Two keys, so that neither feed is ever sent the other's. Not for agents: a version on nuget.org cannot be deleted.
HELP
    exit 0
fi
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

if [[ $# -ne 1 || "$1" == -* ]]; then
  echo "usage: agent-publish-local.sh <version>   (just agent-publish-local <version>; AGENTS.md, section 12)" >&2
  exit 2
fi
version="$1"
"$ROOT/scripts/.internal/agent-sdk-version.sh" "$version"

cd "$ROOT"
if [[ -f .env ]]; then
  # Why: not exported, unlike for `just server`: the key reaches the push below and nothing else
  # shellcheck disable=SC1091
  . ./.env
fi

feed_url="${AGENT_SDK_FEED_URL:-}"
feed_key="${AGENT_SDK_FEED_API_KEY:-}"
if [[ -n "$feed_key" && -z "$feed_url" ]]; then
  # Why: without this a leftover key would turn a push meant for a private feed into a public, permanent one
  echo "AGENT_SDK_FEED_API_KEY is set without AGENT_SDK_FEED_URL: in .env set both to publish to that feed, or remove the key to publish to nuget.org." >&2
  exit 1
fi

if [[ -n "$feed_url" ]]; then
  source="$feed_url"
  key="$feed_key"
  key_name="AGENT_SDK_FEED_API_KEY"
  key_help="the push key of $feed_url"
  # Why: the symbol package is for nuget.org's symbol server; another feed may refuse it
  symbols=(--no-symbols)
else
  source="https://api.nuget.org/v3/index.json"
  key="${AGENT_SDK_NUGET_API_KEY:-}"
  key_name="AGENT_SDK_NUGET_API_KEY"
  key_help="a key from https://www.nuget.org/account/apikeys, scope Push, glob Suwageval.AgentSdk"
  symbols=()
fi
if [[ -z "$key" ]]; then
  echo "$key_name is not set: nothing was packed or pushed. Add it to .env at the repo root ($key_help), or release with just agent-publish, which needs no key." >&2
  exit 1
fi

dotnet pack Suwageval.AgentSdk -c Release -o .artifacts/nupkg -p:Version="$version" -p:ContinuousIntegrationBuild=true
echo "pushing Suwageval.AgentSdk $version to $source"
dotnet nuget push ".artifacts/nupkg/Suwageval.AgentSdk.$version.nupkg" --source "$source" --api-key "$key" "${symbols[@]}"
