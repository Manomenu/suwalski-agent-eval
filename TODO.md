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

## Later, after the MVP

- **Monitoring (live traffic) — built on `epic/monitoring-tab`**, following [the plan](https://claude.ai/artifact/Y7FDEQtqn36YaaCUwA4p2e):
  `features` in `agent.yaml`, `dataVersion` out of the version's hash, the evaluation API under `/api/eval/…` (the old addresses
  removed), conversations sent by a deployed agent (`POST /api/monitoring/turns`, the .NET SDK's `IConversationRecorder`), the
  Monitoring page, `suwageval/monitoring-checks.yaml` run on every closed conversation by `MonitoringWorker` with 7/30-day rates and
  daily charts, Versions' `monitoring` usage (Current, Not evaluated, `?environment=`), a scenario drafted from a conversation as it
  is or replayed to a turn with a simulated user, and the old version filters gone (`versions --archived`, `--any-group`,
  `--all-versions`, the API's `grouping` of versions, `GroupRunsOnly`, the web's `?versions=`). The example agent sends its `/chat`
  conversations. **Before merging:** announce the breaking change (an installed CLI from before the epic calls the old addresses and
  flags), and update the bots to the new CLI. What is left, not in the first version:
  - **A version's page with two panels side by side:** Evaluation (runs, pass rate) and Monitoring (conversations, the monitoring
    checks' pass rate) — the plan's "Strona wersji".
  - **A daily limit of judge calls** (`Monitoring:MaxJudgeCallsPerDay`; over it a conversation is checked without the judge and
    marked) and a `validate` warning for a monitoring check that would fail nearly every real conversation.
  - **`sample` and `keepDays`** under `features.monitoring`: the share of conversations the SDK sends (drawn by `conversationId`),
    and after how many days a conversation's content goes while its check results stay. Until `keepDays`, conversations are kept.
  - **A `Redact(turn)` hook in the SDK**, with the first agent that handles personal data, together with what the server keeps and
    for how long.
  - **Which checks a conversation gets**, only when running all of them becomes a problem (cost, too many checks): (a) retrieval
    over the checks' descriptions in a vector store, or (b) triage by tags a model picks; (b) needs no new component.
  - Alerts on a falling pass rate, scaled scores, the agent's own check types (below).

- **Check types defined by the agent** (Langfuse's code evaluators, the black-box way). Besides the built-in check types, the
  agent declares its own in its served contract: a name (`mentioned-xd-twice`), a `description`, the JSON Schema of its
  arguments and the path of an endpoint. A scenario uses it like any other type (`mentioned-xd-twice: { word: xd }`);
  `validate` checks the name and the arguments against the served contract (it reads it already). Suwageval calls the
  endpoint with the conversation's log and the arguments, and gets a verdict of the usual shape (passed, message, evidence).
  The SDK gives the interface to implement and `MapSuwagevalAgent` maps the endpoint, as it does the turn; the web shows the
  check as YAML like the others, marked as the agent's own type. Why this and not code written in Suwageval's UI: the logic
  lives in the agent's language and can read the application's own data (did the order really get created) — a sandboxed
  evaluator only sees the trace — and Suwageval gets no sandbox, no new component. The same types serve the live checks
  above.
  - **Repeatability:** the check's code can change without the contract changing, so results of "the same version" would
    stop being comparable. Each type carries a `version` in the contract, and it goes into the version's hash.
  - **`recheck` is no longer offline** for these checks: it needs the agent reachable; without it they are skipped and the
    output says so, not failed.
  - **The agent judges itself:** the check runs in the agent's process, so a bug there can break the check too. A known
    trade-off, said in the docs; the endpoint may live in a separate service, the contract only names its address.
  - Open questions: a timeout and what a failed call means (an error of the check, like an agent error, never a fail), the
    concurrency limit (the run's own), CLI parity (`scenario` and `agent` print the type's definition).

  - **The pilot: `obeyed-scope-policy` on monitored conversations.** The agent has an internal scope policy (what it may answer,
    what it must refuse or hand over); a check that every monitored conversation obeyed it shows whether that internal logic fails
    on real traffic. Who judges it is open — three ways, none chosen yet:
    1. **An agent-defined check type:** the agent's own endpoint decides, with its own policy code. Close to the policy, but the
       agent judges itself: a bug in the policy code can pass both the answer and the check.
    2. **A `judge` with extra context written in the repository:** the check gets `context` with the policy text — inline in the
       YAML (copied, drifts from the agent's real policy) or a file next to the scenarios (`context: { file: policies/scope.md }`,
       pushed with the checks, so it is versioned with them, but still a copy of what the agent uses).
    3. **A `judge` with context from the agent's contract:** the agent declares its policy in its served contract (a named
       document, e.g. `documents: { scopePolicy: … }`), so the policy is part of the version's hash, and the check names it
       (`judge: { criteria: "…", context: { contract: scopePolicy } }`); the judge reads it from the contract of the version that
       answered the conversation. The check depends on more than the YAML, but on something versioned and served by the agent
       itself, never on a copy. The judge stays independent of the agent's code.
    The doubt: 2 and 3 make a check depend on something outside its YAML file. 3 looks the most solid (one source, versioned,
    independent judge); decide with the first agent that has such a policy, and with it whether `context` is a general field of
    `judge` (and of `$judge`).
- **Scored checks, not only pass/fail** (Langfuse's numeric, categorical, boolean and text scores). A check could return a
  value — a number (a judge's 1–5, a similarity), a category (`polite` / `neutral` / `rude`) — with a threshold or a list of
  accepted values that turns it into passed or failed. Everything built on the verdict stays binary and unchanged: samples
  and `passThreshold`, regressions, unstable checks; the value adds averages and a trend per check, and the comparison can show
  how far a value moved, not only that it flipped. Fits a `judge` check (its criteria ask for a score) and the agent's own
  check types above. Open questions: the shape in contract v1 and in the scenario YAML (`score: { min: 4 }`,
  `accept: [polite, neutral]`), how samples aggregate a value (mean, or the share of passing samples as today), where the
  values are shown on the run page.

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
  per model in `LanguageModels/ModelClients.cs`) is what the judge and the user simulator use. Still open: more than one run at a time (`RunWorker` takes one);
  the agent under test calls its own model, possibly the same server, which Suwageval cannot
  count — the per-run limit is what protects it.
- **Judge: later.** A per-model API key (the TODO "Secrets" checklist, when a model asks for one);
  judging with several models or a panel; caching verdicts of identical (criteria, output) pairs;
  streaming through `LimitedChatClient` (it refuses streaming until a caller needs it).
- **A check on the state a call was made in** (what is left of the multi-agent contract, whose
  rest — agents, `by`, `agent` on a call — is built): the state at any point is the `data` of the
  last `state.changed` event before it, read from the log, not stamped on the call. A check can
  then say `where: { state: { node: awaiting_payment } }` — one source of truth, nothing the
  agent has to repeat on every call. Built with its first user.

- **Sample profiles.** `samples` and `passThreshold` are set per scenario or check today; when the same pair keeps repeating,
  named presets (`noisy`: 5 samples, 80%) assigned by name like a tag.
- **Judge control.** A person can mark a model-judged verdict (a `judge` check or a `$judge` matcher) as wrong on the run
  page; Suwageval keeps these marks and shows how often the judge is wrong per check and per criteria, so a `judge` check can
  be trusted or its criteria fixed. Open questions: where the mark lives (per sample verdict), whether a marked verdict changes
  the run's result (probably not — a separate "judge accuracy" view), and the CLI parity (a command to mark and to list).
- **Reflection on failed checks: the agent's fault or the check's?** Started by a person on a finished run (a button on the
  run page, `suwageval reflect <runId>` in the CLI, the plugin offering it after a failed run). For each check that did not
  pass, a model (the judge's pool, `Models:`) reads what the verdict came from — the scenario's conversation, what the agent
  answered and did (tool calls, events, retrieved sources), the check's definition, the verdict's message and evidence, and
  the check's history (regression state, unstable, agent errors) — and says which it most likely is: **the agent got it
  wrong**, **the check is wrong** (too strict, a `$pattern` or criteria that misses a valid answer, an outdated expectation
  after an intended change), **the scenario is ambiguous**, **judge noise** (a model-judged check flipping), or **not the
  agent's answer at all** (an agent error, a simulated user that could not be played). With its reasoning and a suggested fix:
  a change to the check (what to edit in the YAML) or a hint at what the agent did wrong. It never changes a verdict or a
  regression state — it is advice, stored with the run and shown next to each failed check; a person decides (the fix goes
  through the repo's files and `push`, as always; a fixed check is confirmed with `recheck`).
  - **Over several runs** (the comparison or a group) it has a signal one run lacks: a check failing in every variant points
    at the check or the scenario, one failing in a single variant at that variant of the agent. Worth it once the single-run
    reflection proves useful; the comparison page is where it would start.
  - Open questions: the cost (a model call per failed check; a cap per run), whether its classes feed "Judge control"
    (a reflection saying "judge noise" is a candidate mark), and how it differs from what Claude Code already does locally with
    `/suwageval:fix` (this one works on the web and for anyone, with the run's whole history at hand).
- **Cost and tokens per run.** The agent's contract could report token usage per turn (an optional field in the turn answer),
  and the judge's calls are Suwageval's own; show tokens and an estimated cost per run, per scenario and in the trends — samples
  default to 3, so the calls tripled and the cost should be visible. Open questions: the field in contract v1 (an optional
  `usage` on the turn answer), a price table per model (config), the judge's cost shown separately.
- **Reports: a PDF without a person.** The print of the run page and of the comparison is built (the "Print" button and
  `results`/`compare --open --print`, `docs/front-doors.md`): the browser's dialog is the preview and gives "Save as PDF". What is left is
  a file made with nobody at the screen (a report in CI): the server prints the same web view to PDF with a headless Chromium, as the
  sibling project's `scripts/.internal/package.sh` does for its documents (a new component in the image, compose and the chart: say why),
  through `GET /runs/{id}/report.pdf` and `GET /runs/compare/report.pdf?…` (the views' parameters), and the CLI saves it: `suwageval
  results <runId> --report [path]`, `suwageval compare … --report [path]` (a file or a folder; without it the current folder and a name
  that says what it is). The comparison keeps the cap of the print (the chunks of columns were dropped in its favour: more runs than fit
  a page are compared in smaller sets); the answer above it is 422, the CLI refuses before asking. The plugin lists it, and Claude
  saves a report only when asked. Build it when a report is wanted in CI.
- **Later, only if this proves useful:** Suwageval sending its verdicts to Langfuse as scores per trace (an outside service, so a
  live test, the daily check and a secret, AGENTS.md section 3); Suwageval reading traces back; a `traceparent` header so the
  agent's spans join a trace Suwageval starts.
- **`MSBUILDDISABLENODEREUSE=1` in the owner's environment — unsure, probably not needed.** `Directory.Build.rsp` (`-nodeReuse:false`)
  already stops the idle MSBuild nodes of every command-line build in this repository (the agents' builds, `dotnet watch`, the
  gate), which is what piled up to gigabytes. What it does not reach: VS Code's C# Dev Kit, which builds through the MSBuild API
  (design-time builds after a branch switch or a project change) and keeps its own nodes — a handful, a few hundred MB, seen on
  2026-10-24. Only the environment variable reaches those, set for the whole desktop session (`~/.config/environment.d/`, then
  log in again). Its costs:
  - **Every .NET project of the owner**, not only this one: each build starts its worker processes again instead of reusing warm
    ones — a second or two per build on the command line, more where builds are many and small, which is what the editor's
    design-time builds are: the project load after a branch switch and IntelliSense refreshes can feel slower.
  - **Hidden and global:** a setting outside every repository that changes how builds behave; forgotten, it is hard to trace, and
    it must be set again on a new machine.
  - **It treats a symptom that is small now:** idle reused nodes end by themselves after a while; the large pile came from many
    parallel command-line builds, which the `.rsp` already prevents.
  Take it only if the editor's nodes are seen piling up again; until then `dotnet build-server shutdown` and closing the idle
  nodes by hand is enough.
- **Low priority, unsure it is worth it: tags matched by the system, not written in the scenario.** A tag would be defined
  once per agent with a description (`refunds: anything about returning money`), and a model assigns the most relevant tags
  to each scenario (on `push`, stored, shown as assigned, a tag written in the YAML still wins). Either another behaviour of
  the existing tags or a parallel mechanism beside them (e.g. `topics`). The same agent-scoped tag list would serve the triage
  of live checks above. The doubt: written tags are free, repeatable and reviewed in the repo; matched ones can change between
  pushes and make the filters and the trend lie. Build only if writing tags proves a burden.
