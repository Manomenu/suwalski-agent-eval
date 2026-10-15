# TODO

Decided, not built yet: each item is done in the change that first needs it (AGENTS.md,
section 0).

## Secrets — with the first real secret (the model provider's API key, roadmap step 4)

Secrets never go into `appsettings*.json`: those files are committed. Nothing copies or
generates secret files into the repo — the app reads them where they live.

| Where | Where the app gets a secret |
| --- | --- |
| laptop, `just server` | `dotnet user-secrets` — `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`, outside the repo; ASP.NET loads it in Development on top of appsettings. Set with `dotnet user-secrets set "Model:ApiKey" "…" --project AgentEval.Server` |
| laptop, `just up` | the same file: `compose.yaml` mounts `~/.microsoft/usersecrets` read-only into the server container (as Visual Studio's Docker tooling does) and the server loads user secrets there too |
| cluster | environment variables from a Kubernetes Secret (`Model__ApiKey`), created by the platform repo's `setup.sh`, read by the chart through `envFrom` |
| CI | none — the gate never calls a model |

To do in that change:

- [ ] `<UserSecretsId>` in `AgentEval.Server.csproj` (`dotnet user-secrets init`).
- [ ] The options class of the feature that needs the secret, bound from configuration,
      validated with `ValidateOnStart` — no default value for the secret.
- [ ] `compose.yaml`: mount `~/.microsoft/usersecrets` read-only; load user secrets in the
      container (`AddUserSecrets<Program>(optional: true)` outside Development, or run the
      container with `ASPNETCORE_ENVIRONMENT=Development`) — pick one and say why.
- [ ] `scripts/.internal/secrets.sh`: back up and restore that one `secrets.json` to Bitwarden
      (folder Homelab, note `agenteval/user-secrets`) instead of `.env` files; `restore`
      writes it with mode 600 and keeps a differing file unless `--force`.
- [ ] Chart: the Secret's name as a value, `envFrom` on the server; the prompt in the
      platform repo's `setup.sh`; tell the owner to run it and `just secrets backup` there.
- [ ] AGENTS.md, section 7: replace the `.env` description with the above.

## Decided for the slices to come

The design lives in the Claude artifact "agenteval — organizacja projektu i API"
(`docs/claude-artifacts/README.md`), section "Decyzje"; each item lands with the slice it
belongs to.

- **Tags (Scenarios):** a run picks scenarios by tag, no "suite" entity.
  `POST /agents/{agentId}/scenarios/tags` with `{ scenarioKeys, add, remove }` sets and
  removes tags on a list of scenarios at once. Tags are metadata: changing them makes no new
  revision.
- **The agent under test (Invocation):** first an external agent over HTTP, contract v0:
  `POST {agentUrl}/agenteval/turn` with `{ conversation }` → `{ items }` (the turn's tool
  calls, tool results, events and final message, as log items with `kind`). The agent runs
  its own tools against test data. Example: `~/repos/suwalski-example-agent` (Python,
  Microsoft Agent Framework). An agent built inside agenteval from an `AgentVersion` may
  come later as a second way.
- **Who calls the API from an agent's repo:** for now JSON files in that repo
  (`agenteval/agent-version.json`, `scenarios/`, `checks/`) and a script. Target: a CLI
  `agenteval` (`push` a folder = versions, scenarios, checks; `run --tag … --wait` with an
  exit code for CI), a CI step around it, and the Claude Code plugin over MCP.
- **Non-determinism (Runs, Checks):** one sample by default; `samples: N` on a scenario
  (the conversation is repeated, not the check), `passThreshold` on a check (default 100%)
  over its pass rate. Later, when the same settings repeat: profiles — named presets
  (`noisy`: 5 samples, 80%) assigned by name like a tag.

## Later, after the MVP

- **Editor hints for the scenario files:** publish a JSON Schema of the YAML files an agent's
  repo keeps (the agent, its scenarios with their checks) and point VS Code's YAML extension
  at it, so fields are suggested and mistakes underlined while writing. Until then the CLI's
  `validate` command is the check.
