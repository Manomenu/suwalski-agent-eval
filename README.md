# agenteval

A harness for evaluating an LLM agent system: replay a recorded conversation, let the agent
answer its last message, and check that answer with tests — deterministic ones and an LLM
judge.

**Status:** the repository is initialised (API, web app, database, gate, CI, containers,
chart) and has no evaluation feature yet. Everything under "What it is for" is the intent the
features will be built from, not something that runs today.

## What it is for

### A scenario

A scenario is a frozen moment of a conversation with the agent, plus what we expect next:

- **The context, structured** — everything the agent had at that point:
  - the agent's configuration: system prompt, the tools it may call (their names and schemas);
  - the messages so far: user, assistant, system;
  - the tool calls already made: which tool, with which arguments, what it returned;
  - the data retrieved by RAG: the query and the chunks that came back (text, source, score);
  - the last message — the user's turn the agent answers next.
- **The tests** run on the agent's answer to that last message.

### A run

The agent under test receives the context and produces its next turn: a tool call, an answer,
or both. Tools are not executed for real — what they would return comes from the scenario, so
a run is repeatable and touches nothing outside.

### The tests

Run after the last message, on what the agent produced:

- **Deterministic** — no model involved, so cheap and exact:
  - the right tool was called: its name, its arguments (exact, a subset, or a matcher), the
    order and number of calls;
  - a tool that must not be called was not;
  - the answer contains / does not contain a text, matches a pattern, fits a format (e.g. a
    JSON schema).
- **LLM judge** — a model grades the answer against a rubric:
  - the answer contains the essential information (e.g. "states the refund period of 14 days");
  - the answer is grounded in the retrieved RAG chunks — nothing made up beyond them;
  - language, tone, what it must not promise.

  Each verdict carries pass/fail, a short justification and the evidence it relied on.

### The results

Per test: pass/fail and why. Per scenario: all its tests. Per run of a suite: the agent's
configuration and model, the judge's model and prompt version, the totals — and a comparison
with an earlier run, so a regression after a prompt or model change is visible at once.

### Open questions

- **The scenario format:** a JSON schema of our own, an import from existing traces
  (OpenTelemetry GenAI conventions, Agent Framework's serialized threads), or both?
- **The agent under test:** built in-process with Microsoft Agent Framework from the scenario's
  configuration, or an external system called over HTTP?
- **Tool results during a run:** stubbed from the scenario, or does the run stop at the first
  tool call (enough for "was the right tool called")?
- **Non-determinism:** one sample per scenario, or several with a pass rate?
- **The model providers** for the agent and the judge, and the cost limits of a run.

### Roadmap (a sketch, one feature at a time)

1. **Scenarios** — the model, a JSON import, storage in PostgreSQL, a list on the web.
2. **Deterministic tests** on a given answer — no model yet, fully covered by the gate.
3. **Runs** — the agent under test through Microsoft Agent Framework produces the answer.
4. **Judge** — LLM-as-judge tests, their verdicts stored with the run.
5. **Results** — run reports and the comparison between runs on the web.

Microsoft Agent Framework enters the repository with step 3, as the first code that uses it
(AGENTS.md, section 0) — together with its live test and the recorded answers the gate uses
instead of a model.

## Stack

- **Server:** .NET 10, ASP.NET Core minimal APIs, Npgsql with plain SQL and numbered
  migrations, OpenAPI document the web app is typed from; Microsoft Agent Framework for the
  agent under test and the judge (from step 3).
- **Web:** React 19, Mantine, Vite, TypeScript strict.
- **Around them:** PostgreSQL 17, a container per part, compose for the whole stack on a
  laptop, a Helm chart with a post-deploy smoke test for the cluster.

The repository follows [solid-app-tpl](https://github.com/Manomenu/solid-app-tpl) — the same
layout, rules and gates, translated from Python to .NET.

## Requirements

.NET SDK 10, `pnpm` (via corepack), `just`, `podman` with `podman compose` (or `docker` with
`docker compose`); for the full gate also `helm`, `shellcheck` and `gitleaks`. After cloning:
`just sync` (dependencies, the C# formatter and the git hooks).

## Everyday commands

```sh
just                 # every recipe, grouped
just db up           # local PostgreSQL on :5453
just server          # API on :6220 (OpenAPI document at /openapi/v1.json)
just web             # web app on :3220, proxies /api to the server
just up              # the whole stack in containers on :8092 — no cluster needed
just check           # the quality gate, exactly what CI runs
just e2e             # browser tests against a real server and database
just secrets backup  # copy the local .env files into Bitwarden (restore on a new machine)
```

How the repo is organised and what every change must bring along: [AGENTS.md](AGENTS.md).
