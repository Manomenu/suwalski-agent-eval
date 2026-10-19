# TODO

Decided, not built yet: each item is done in the change that first needs it (AGENTS.md,
section 0).

## Secrets — with the first real secret (the model provider's API key, roadmap step 4)

The judge's API key (`Models:<name>:ApiKey`) is read today from the gitignored `.env`, like its
address. This section moves it, with every later secret, to where the table below says.

Secrets never go into `appsettings*.json`: those files are committed. Nothing copies or
generates secret files into the repo — the app reads them where they live.

| Where | Where the app gets a secret |
| --- | --- |
| laptop, `just server` | `dotnet user-secrets` — `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`, outside the repo; ASP.NET loads it in Development on top of appsettings. Set with `dotnet user-secrets set "Model:ApiKey" "…" --project Suwageval.Server` |
| laptop, `just up` | the same file: `compose.yaml` mounts `~/.microsoft/usersecrets` read-only into the server container (as Visual Studio's Docker tooling does) and the server loads user secrets there too |
| cluster | environment variables from a Kubernetes Secret (`Model__ApiKey`), created by the platform repo's `setup.sh`, read by the chart through `envFrom` |
| CI | none — the gate never calls a model |

To do in that change:

- [ ] `<UserSecretsId>` in `Suwageval.Server.csproj` (`dotnet user-secrets init`).
- [ ] The options class of the feature that needs the secret, bound from configuration,
      validated with `ValidateOnStart` — no default value for the secret.
- [ ] `compose.yaml`: mount `~/.microsoft/usersecrets` read-only; load user secrets in the
      container (`AddUserSecrets<Program>(optional: true)` outside Development, or run the
      container with `ASPNETCORE_ENVIRONMENT=Development`) — pick one and say why.
- [ ] `scripts/.internal/secrets.sh`: back up and restore that one `secrets.json` to Bitwarden
      (folder Homelab, note `suwageval/user-secrets`) instead of `.env` files; `restore`
      writes it with mode 600 and keeps a differing file unless `--force`.
- [ ] Chart: the Secret's name as a value, `envFrom` on the server; the prompt in the
      platform repo's `setup.sh`; tell the owner to run it and `just secrets backup` there.
- [ ] AGENTS.md, section 7: replace the `.env` description with the above.

## Decided for the slices to come

The design lives in the Claude artifact "suwageval — organizacja projektu i API"
(`docs/claude-artifacts/README.md`), section "Decyzje"; each item lands with the slice it
belongs to.

- **Tags (Scenarios):** a run picks scenarios by tag, no "suite" entity.
  `POST /agents/{agentId}/scenarios/tags` with `{ scenarioKeys, add, remove }` sets and
  removes tags on a list of scenarios at once. Tags are metadata: changing them makes no new
  revision.
- **The agent under test (Invocation):** first an external agent over HTTP, contract v1:
  `POST {agentUrl}/suwageval/turn` with `{ conversation }` → `{ items }` (the turn's tool
  calls, tool results, events and final message, as log items with `kind`). The agent runs
  its own tools against test data. Example: `~/repos/suwalski-example-agent` (Python,
  Microsoft Agent Framework). An agent built inside suwageval from an `AgentVersion` may
  come later as a second way.
- **Who calls the API from an agent's repo:** for now JSON files in that repo
  (`suwageval/agent.yaml`, `suwageval/scenarios/*.yaml`) and a script. Target: a CLI
  `suwageval` (`push` a folder = agent, scenarios, checks; `run --tag … --wait` with an
  exit code for CI) and a CI step around it. (The Claude Code plugin driving the CLI exists: `claude-plugin/`.)
- **Non-determinism (Runs, Checks):** one sample by default; `samples: N` on a scenario
  (the conversation is repeated, not the check), `passThreshold` on a check (default 100%)
  over its pass rate. Later, when the same settings repeat: profiles — named presets
  (`noisy`: 5 samples, 80%) assigned by name like a tag.

## Later, after the MVP

- **Scenarios from real conversations of a connected agent** (the owner's idea). The agents send their conversations
  (the context they had, the turns, tool calls and results) to Suwageval, kept per agent version; on the web a person picks a
  conversation and turns it into a scenario. Two parts to design: the integration — how an agent system sends its
  conversation logs (an endpoint the agent calls, or an import from its traces, e.g. Langfuse/OpenTelemetry), with what
  retention and what to strip (personal data); and the way back into the agent's repository, since Suwageval runs on another
  machine than the repo: the page gives the scenario as ready YAML with a "Copy YAML" button and the file's path
  (`suwageval/scenarios/<key>.yaml`) with its own copy button — and/or a CLI command run in the agent's repo that writes
  the file there (`validate`, then `push`).

- **Runs from an agent's CI** (needs Suwageval on the cluster first, and the CLI speaking Cloudflare Access with a
  service token). Not a run on every PR: a series of small PRs, or one big MR split into many, would start the whole
  evaluation again and again, each time calling the agent's model and the judge, while most PRs (refactors, tests, CI, docs)
  do not change what the agent answers. Triggers, in this order:
  1. **On demand in a PR** — a label (`eval`) or a comment (`/suwageval`): the person decides, not the number of PRs.
  2. **After a merge to the agent's `master`** — one full run per change that landed, compared with the previous one; a
     split MR is evaluated once it is whole.
  3. **Later, per agent repo: paths that change behaviour** (prompts, tool definitions, model config, the RAG pipeline)
     start a run on their own; the list lives in that repo's workflow.
  4. **Skip a version already evaluated** — the agent's version is its contract hash: when it has a recent finished run,
     CI answers "already evaluated" with the link instead of running. Only together with 3: a tool's implementation can
     change without its schema, so the contract (and the version) stays the same.
  The result goes back as a PR comment: totals against `master`, the tags that moved, checks that got worse (stable ones
  first, unstable ones marked as possible noise), links to the comparison and the version diff. Open question: how the
  server reaches the agent built from a PR (started in the job behind a tunnel, or a preview deployment).

- **Regressions, the automatic mode** (designed, to be built first; the artifact "Regresje w Suwageval",
  `docs/claude-artifacts/README.md`). A regression is a check that passed steadily and fails now; the baseline is the
  check's history over the newest reference runs (runs from a branch of `regression.branches` in `agent.yaml`, default the
  repo's default branch, with a clean tree and a pushed commit), so nobody sets or marks anything. Open decisions are at
  the end of the artifact.

- **Regressions, the manual mode** — only if the automatic one proves not enough in practice: marking a run as the
  baseline by hand, `expected fail` on a check (with a reason or an issue link), accepting a change by hand. Left out
  of the automatic mode on purpose: each is a step someone has to remember, and a forgotten one makes the report lie.

- **`validate`: JSON Schema checks of free JSON.** `arguments` of a `ToolCall` and `data` of an
  `Event` (in the log, and `args` / `data` of the checks) are not yet validated against the
  `parameters` / `dataSchema` the agent declares in its served contract (`validate` already reads
  it when the agent is reachable; the run could check what the agent produces too). Needs a JSON
  Schema validator package — add
  it with this first check.
- **`validate`: one problem per element.** The contract reports the first mistake in what it
  reads, so each item, check and tool is read on its own (several problems per file), but two
  mistakes inside one check show up one after the other.

- **Editor hints for the scenario files:** publish a JSON Schema of the YAML files an agent's
  repo keeps (the agent, its scenarios with their checks) and point VS Code's YAML extension
  at it, so fields are suggested and mistakes underlined while writing. Until then the CLI's
  `validate` command is the check.
- **Parallel runs, the first level** (`Runs:MaxParallelScenarios`, built) **and the second:** the
  per-model pool (`Models:<name>:MaxConcurrency`, built with the judge: one `LimitedChatClient`
  per model in `Models/ModelClients.cs`) is what the judge uses today; the user simulator uses
  the same pool when it arrives. Still open: more than one run at a time (`RunWorker` takes one);
  the agent under test calls its own model, possibly the same server, which Suwageval cannot
  count — the per-run limit is what protects it.
- **Judge: later.** A per-model API key (the TODO "Secrets" checklist, when a model asks for one);
  judging with several models or a panel; caching verdicts of identical (criteria, output) pairs;
  streaming through `LimitedChatClient` (it refuses streaming until a caller needs it).
- **Multi-turn evaluation with a simulated user** (a design note; not to be built now). More than one agent turn is reserved
  for a user played by an LLM, who answers each real reply of the agent. The scenario says what that user wants to achieve and
  how they behave — e.g. tries to hack the system, or is frustrated and wants a refund — through `continuation`: `turns` plus a
  goal/persona description. Fixed, scripted user messages are deliberately NOT supported: a message written before the agent's
  real answer is known may not fit that answer, and the scenario then measures noise. A conversation of several steps is split
  into several scenarios instead, each with the earlier agent answers predefined in its `conversation`. This is a different kind
  of evaluation than regression scenarios, which do not need it. It calls a model, so it
  arrives with the outside-service rules of AGENTS.md section 3 (a live test, the daily check from the cluster, degrade rather
  than break) and with the per-model pool the judge already uses; until then `continuation.turns` above 1 fails the sample without a
  call.
  It needs a conversation key in the request. `TurnRequest` carries none today, so an agent that stores its own conversation
  (a database row per conversation, a state machine) can only rebuild it from the sent log on every call, and loses whatever
  the log does not carry; the whole conversation is then judged on an agent that forgets part of its state each turn. The
  request gets a key that stays the same for every turn of one sample (e.g. `conversationKey`): the agent continues the
  conversation it stored under that key and refuses a log that is not what it holds plus the new user message. The
  correlation headers are not that key, since they must not change behaviour.
- **A check on the state a call was made in** (what is left of the multi-agent contract, whose
  rest — agents, `by`, `agent` on a call — is built): the state at any point is the `data` of the
  last `state.changed` event before it, read from the log, not stamped on the call. A check can
  then say `where: { state: { node: awaiting_payment } }` — one source of truth, nothing the
  agent has to repeat on every call. Built with its first user.

- **Judge control.** A person can mark a model-judged verdict (a `judge` check or a `$judge` matcher) as wrong on the run
  page; Suwageval keeps these marks and shows how often the judge is wrong per check and per criteria, so a `judge` check can
  be trusted or its criteria fixed. Open questions: where the mark lives (per sample verdict), whether a marked verdict changes
  the run's result (probably not — a separate "judge accuracy" view), and the CLI parity (a command to mark and to list).
- **Cost and tokens per run.** The agent's contract could report token usage per turn (an optional field in the turn answer),
  and the judge's calls are Suwageval's own; show tokens and an estimated cost per run, per scenario and in the trends — samples
  default to 3, so the calls tripled and the cost should be visible. Open questions: the field in contract v1 (an optional
  `usage` on the turn answer), a price table per model (config), the judge's cost shown separately.
- **A note on a run, editable after it finished.** The only thing worth labelling after it is stored: a version's label comes
  from the running agent (its contract) and scenario tags from the YAML, so editing them in Suwageval would contradict their
  source; a run is a record of what happened, and a free-text note says why it was started ("rebuilt the retrieval index",
  "trying model X"). Shown on the run page, in the runs list and in the comparison; set on the web and with the CLI
  (`suwageval run --note …`, and a command to change it later). It changes no verdict and no regression state — it is not a
  baseline mark (that is the manual mode above). Build it when comparing runs shows the missing "why".
