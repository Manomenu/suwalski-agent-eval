<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/visual/board/img/readme/hero-dark.png">
  <img alt="Suwageval — regression tests for LLM agents before a release, and checks on their real conversations after it. Scenarios as YAML, regressions not noise, production monitoring, a Claude Code plugin. A sample run: gives-tracking-link is a regression (passed 5 times, fails now), declines-politely is unstable, mentions-carrier is fixed; exit 1 for one regression. 2 areas (Evaluation and Monitoring), 7 check types (one a model judge), 3 of 8 check states fail CI, any agent language (two HTTP routes)." src="docs/visual/board/img/readme/hero-light.png" width="100%">
</picture>

**Suwageval** tests LLM agents the way unit tests test code. Scenarios — a conversation plus checks on the agent's next
answer — live as YAML in the agent's own repository. Suwageval sends them to the running agent several times each, judges the
answers and tells you after a prompt or model change **what got worse, and what is only noise**. A deployed agent can also send
its real conversations: Suwageval checks every one of them and turns a bad one into a scenario.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/visual/board/img/readme/why-dark.png">
  <img alt="Why. Without Suwageval: a prompt change ships after a few chats by hand; one red test could be a real break or a dice roll; 'it worked last week' on an unknown prompt, model and data; production failures stay in logs nobody reads; tests live in a notebook beside the agent. With Suwageval: every scenario runs several times on each change, in CI; each check has a history (regression, unstable or known); every run records the agent's version and data; real conversations are checked daily and a bad one becomes a test; scenarios are YAML in the agent's own repository." src="docs/visual/board/img/readme/why-light.png" width="100%">
</picture>

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/visual/board/img/readme/flow-dark.png">
  <img alt="How it works. 1 Write (you): suwageval/scenarios/*.yaml, a conversation and checks on the answer. 2 Run (evaluation): suwageval run --wait, each scenario times samples against the agent in test mode. 3 Compare (evaluation): with the history on master; regression, new and agent error fail CI, noise does not. 4 Watch (monitoring): the deployed agent sends turns, checks on every conversation, pass rate per day and version. 5 Capture (you): a conversation becomes a scenario, the failure becomes a test. Every step works from the web app, the suwageval CLI, CI exit codes and /suwageval:run in Claude Code." src="docs/visual/board/img/readme/flow-light.png" width="100%">
</picture>

## Quick start

Needs the .NET 10 SDK, [`just`](https://just.systems) and Docker with `docker compose`.

```sh
git clone https://github.com/Manomenu/suwalski-agent-eval && cd suwalski-agent-eval
cp .env.example .env   # set one model: Models__fallback__BaseUrl and Models__fallback__Model
just up                # Suwageval on http://localhost:8093
just cli-install       # the `suwageval` command
```

Suwageval calls a model itself for `judge` checks, simulated users and a user described from a conversation. **One model is
enough**: `Models:fallback` (any OpenAI-compatible server). Each of these roles takes it unless you give the role a model of its own
(`Models:judge`, `Models:simulator`, `Models:describer`, the same three settings).

Your agent serves two routes: `GET /suwageval/contract` (what it is: its LLM agents, models, prompts, tools, events) and
`POST /suwageval/turn` (its answer to a conversation). A .NET agent gets both from the `Suwageval.AgentSdk` package
(`MapSuwagevalAgent`); any other language follows [the contract](docs/scenario-format.md#the-agent-contract-contract-v1).
Then, in the agent's repository:

```sh
suwageval init --suwageval-url http://localhost:8093   # writes suwageval/agent.yaml, asks for the name, the agent's address and whether to monitor it
```

```yaml
# suwageval/scenarios/refund-too-late.yaml
key: refund-too-late
conversation:
  - user: "I bought a kettle 20 days ago and want my money back."
checks:
  - key: no-refund-issued
    type: tool-not-called
    tool: refund_order
  - key: explains-policy
    type: judge
    criteria: Explains that refunds are possible within 14 days and offers what is still possible.
```

```sh
suwageval validate       # the files against the contract the running agent serves
suwageval push           # the agent and its scenarios
suwageval run --wait     # regressions first; exit 1 only on a regression, a new failure or an agent error
```

The same two last lines go into CI.

## What you get

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/visual/board/img/readme/features-dark.png">
  <img alt="What you get. Evaluation, before a release: regressions (each check judged against its own history, CI-ready); unstable checks (flaky checks found per version); compare up to 7 runs side by side against a baseline; version diff of prompts, models and tools read from the agent; simulated users (a model plays a user described in plain words); re-check of stored answers without calling the agent; sweeps (one run per configuration variant, in one group); turn time on every turn and on the trend. Monitoring, after it: conversations per environment, sent by the deployed agent without slowing it; monitoring checks on each closed conversation with pass rates over 7 and 30 days; day-by-day charts of traffic and pass rates with deploys and data changes marked; a conversation copied as a scenario in one click, with the failed checks as comments." src="docs/visual/board/img/readme/features-light.png" width="100%">
</picture>

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/visual/board/img/readme/shot-run-dark.png">
  <img alt="The run page compared with master: 1 checks grouped by state, regressions first with their count; 2 each check's history in the reference runs, oldest first, then this run; 3 one click to the last passing run, side by side; 4 already broken checks, failing since an earlier run and not blamed on this one; 5 known failures folded away." src="docs/visual/board/img/readme/shot-run-light.png" width="100%">
</picture>

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/visual/board/img/readme/shot-trends-dark.png">
  <img alt="Left: unstable checks and the pass-rate trend over runs. Right: what changed between two versions of the agent." src="docs/visual/board/img/readme/shot-trends-light.png" width="100%">
</picture>

Also: `compare` up to 7 runs against a baseline, `recheck` stored answers after fixing a check, `sweep` configuration variants,
and multi-turn **simulation** where a model plays a user you describe in plain words
([docs/scenario-format.md#simulation](docs/scenario-format.md#simulation)).

## Checks and what makes CI fail

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/visual/board/img/readme/checks-dark.png">
  <img alt="Checks: tool-called (the agent called a tool, optionally by which LLM agent and with which arguments); tool-not-called (a forbidden action); event and event-not-emitted (what the agent signalled); cited (the RAG sources the answer used); text (contains, does not contain, matches a pattern); judge (a model decides against criteria in plain words). Any check can be narrowed with where (one turn, after or before a tool or event), and samples with a pass threshold count repeats." src="docs/visual/board/img/readme/checks-light.png" width="100%">
</picture>

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/visual/board/img/readme/states-dark.png">
  <img alt="A failing check is not just red. Passed its last N runs and fails now: Regression, CI fails. Fails with too little history: New, CI fails. The agent gave no answer (HTTP error, timeout): Agent error, CI fails. Started failing in an earlier run: Already broken, CI passes. History mixes passes and failures: Unstable, CI passes. Failing for N runs or more: Known failure, CI passes. Passes after a failing streak: Fixed, CI passes. The history is the newest reference runs: clean, pushed commits on the branches in regression.branches." src="docs/visual/board/img/readme/states-light.png" width="100%">
</picture>

The full list of fields is in [docs/scenario-format.md](docs/scenario-format.md#checks); how a state is decided, in
[docs/concepts.md](docs/concepts.md).

## Monitoring: real conversations

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/visual/board/img/readme/shot-monitoring-dark.png">
  <img alt="The Monitoring page: 1 monitoring checks with pass rates over 7 and 30 days, a red rate links to the failed conversations; 2 traffic and pass rates per day with dashed marks where the version or the data changed; 3 conversations with environment, state, version and checks; 4 a conversation that failed a check." src="docs/visual/board/img/readme/shot-monitoring-light.png" width="100%">
</picture>

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/visual/board/img/readme/shot-conversation-dark.png">
  <img alt="A real conversation that failed a check: 1 the Scenario YAML button, red because a check failed; 2 each monitoring check's verdict with the judge's reasons; 3 the turns as the agent recorded them, with messages, tool calls and the data version." src="docs/visual/board/img/readme/shot-conversation-light.png" width="100%">
</picture>

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/visual/board/img/readme/shot-scenario-dark.png">
  <img alt="The conversation becomes a regression test: 1 copy it whole or up to a chosen user or agent message; 2 the file to save in the agent's repository; 3 a header saying where it came from, with the failed checks at the end as comments." src="docs/visual/board/img/readme/shot-scenario-light.png" width="100%">
</picture>

Switch it on in `agent.yaml`, write the checks, push:

```yaml
# suwageval/agent.yaml
features:
  monitoring:
    closeAfter: 30m                       # a conversation with no turn for this long is closed and checked
    environments: [production, staging]   # which deployments' conversations are taken; none: any
```

```yaml
# suwageval/monitoring-checks.yaml
checks:
  - key: refund-only-when-asked
    description: Refunds an order only when the user asked for that refund.
    type: judge
    criteria: If the assistant called refund_order, the user had asked to refund that very order before the call.
```

In a .NET agent, record each real turn; the SDK queues it and sends it in the background, so the user never waits:

```csharp
builder.Services.AddSuwagevalMonitoring(builder.Configuration);   // Suwageval:Url, :Agent, :Environment
// after answering a real turn:
recorder.Record(conversationId, [userMessage, .. producedItems]);
```

Set `Suwageval__Url`, `Suwageval__Agent` and `Suwageval__Environment` in the deployment (without `Url` the recorder does
nothing). Agents in other languages send `POST /api/monitoring/turns` themselves: [the protocol](docs/scenario-format.md#monitoring-the-conversations-of-a-deployed-agent).

```sh
suwageval monitoring checks                    # pass rates over 7 and 30 days
suwageval monitoring conversations --failed-check refund-only-when-asked
suwageval scenario from-conversation <id> --write   # the conversation as suwageval/scenarios/conversation-<id>.yaml
```

## Versions and settings

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/visual/board/img/readme/shot-versions-dark.png">
  <img alt="The Versions page: 1 usage filter (Evaluation, Groups, Monitoring, Unused, Archived); 2 Current marks the version answering in production now, Not evaluated a version with real traffic and no run; 3 columns for runs, group runs, conversations in 30 days and the last conversation." src="docs/visual/board/img/readme/shot-versions-light.png" width="100%">
</picture>

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/visual/board/img/readme/settings-dark.png">
  <img alt="agent.yaml settings: samples (times each scenario is sent, 1 to 20, default 3); regression.history (reference runs that make a steady pass, 1 to 20, default 5); features.evaluation (scenarios, runs, comparisons, on by default); features.monitoring (store the deployed agent's conversations, off by default); monitoring.closeAfter (quiet time before a conversation is checked, 1m to 24h, default 30m); monitoring.environments (where conversations are taken from, none listed means any). What the agent is (prompts, models, tools) is never written in a file: the running agent serves it." src="docs/visual/board/img/readme/settings-light.png" width="100%">
</picture>

A version is the hash of the contract the agent serves, without its label and its retrieval data, so re-indexing the knowledge
base is not a new version (the run keeps the data it ran on) while a changed prompt, model or tool is. `suwageval versions diff`
shows what changed between two.

## Claude Code plugin

Drive all of it in plain words: the plugin sets the CLI up, writes scenarios, repairs the ones an agent change broke, runs them,
explains what failed and turns a monitored conversation into a scenario.

```
/plugin install suwageval --marketplace Manomenu/suwalski-agent-eval
```

In an agent's repository a session hook gives Claude the loop at the start of every session: what fails now, the conversation
as a scenario, run, find why (in your tracing too, if the project has it), fix the agent rather than the test.
Then ask, or use `/suwageval:scenario`, `/suwageval:fix`, `/suwageval:run`, `/suwageval:from-conversation` — see
[docs/front-doors.md](docs/front-doors.md#use-it-from-claude-code). `/suwageval:update` brings the plugin to the newest `master`;
`/plugin` → **Marketplaces** → `suwalski-agent-eval` → **Enable auto-update** does it by itself.

## How it is built

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/visual/board/img/readme/architecture-dark.png">
  <img alt="The moving parts: your agent in any language serves the contract and answers turns (.NET: Suwageval.AgentSdk); the suwageval CLI, a .NET tool for the repository and CI (validate, push, run, compare, monitoring); the server, ASP.NET Core and PostgreSQL with a run queue, a monitoring worker and versions by contract hash (Docker, Helm); the web app in React and Mantine with Versions, Evaluation and Monitoring pages per agent; the judge and the simulator, OpenAI-compatible models Suwageval calls; the Claude Code plugin that writes and fixes scenarios, runs them and explains failures." src="docs/visual/board/img/readme/architecture-light.png" width="100%">
</picture>

| Run it | Command | For |
| --- | --- | --- |
| on the host | `just db up`, `just server`, `just web` → http://localhost:3230 | developing, rebuilds on save |
| container stack | `just up` → http://localhost:8093 | the same images the cluster runs |
| cluster | the Helm chart in `deploy/chart/` | production, with a post-deploy smoke test |

`just check` runs the gate CI runs (analyzers, formatters, tests against a real PostgreSQL, import contracts, dead code, the
chart); `just e2e` the browser tests. The pictures in this README are drawn on
[docs/visual/board/readme.html](docs/visual/board/readme.html) and rendered with the suwagent plugin's `vdoc.py render`; a
longer visual tour is [docs/visual-readme.html](docs/visual-readme.html). Rules for changing the repository:
[AGENTS.md](AGENTS.md).

## Documentation

| Document | Holds |
| --- | --- |
| [docs/scenario-format.md](docs/scenario-format.md) | every file, the agent contract, every check, simulation, regressions, the monitoring protocol |
| [docs/concepts.md](docs/concepts.md) | scenarios, runs, checks, results and how regressions are decided |
| [docs/front-doors.md](docs/front-doors.md) | the web app, every CLI command, the Claude Code plugin |
| [docs/universal-approach.md](docs/universal-approach.md) | why the contract and the checks look the way they do |
| [docs/architecture.md](docs/architecture.md), [docs/development.md](docs/development.md), [docs/roadmap.md](docs/roadmap.md) | the stack, everyday commands, what comes next |

## License

MIT, © 2026 Piotr Gugnowski — see [LICENSE](LICENSE).
