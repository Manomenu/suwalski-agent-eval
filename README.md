# Suwalski Agent Eval (suwageval)

Suwageval is a harness for evaluating an LLM agent system: replay a recorded conversation, let the agent
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

### Front doors: the web, a CLI, and Claude Code through the CLI

The server has one interface, its HTTP API. Three users reach it:

| Who | Through | Does |
| --- | --- | --- |
| a person in the browser | the web app | browses agents, scenarios, runs, results, comparisons |
| an agent's repository, by hand or in CI | the `suwageval` CLI (`Suwageval.Cli`) | `validate` the scenario files, `push` the agent, its scenarios and checks, `run --tag … --wait` with an exit code for CI, `results <runId>` to open a run on the web, `versions` to list, archive and restore the agent's versions |
| Claude Code | the Claude Code plugin in this repo (`claude-plugin/`) that drives the same CLI, like suwgit's | "build a scenario for this agent from this conversation", "run the regression tag on the new version", "why did scenario X fail" — writing the scenario as a file in the agent's repo, then `validate` and `push` |

- **Anything the web can do, the CLI — and so Claude Code through the plugin — can do too; not the
  other way round.** The CLI may do things the web cannot (push scenarios, say). A new action on
  the web arrives with its CLI command and the plugin's knowledge of it, in the same change.
- **The scenario files live in the agent's repository**, in `suwageval/`: `agent.yaml` (the
  agent: `name` and `endpointUrl`, nothing else) and
  `scenarios/*.yaml` (a scenario with its checks inside; `user:` / `assistant:` shorthands for
  the log). Suwageval's address is not in them: `SUWAGEVAL_URL` (default
  `http://localhost:3230`) or `--server` — the web app's address, the API is under its `/api`
  (so on a laptop the CLI needs `just web` running too).
- **The results are read on the web.** `suwageval run --wait` prints one line of totals and the
  link to the run's page, with its exit code; `suwageval results <runId>` opens that page in
  the browser. For machines, `--json` on either prints the results (and opens nothing).
- **A version is never written in a file: the running agent serves it.** `GET
  {endpointUrl}/suwageval/contract` returns its system prompt, model, tools (with argument
  schemas) and events (with data schemas), and a label. `suwageval run` makes the server read it,
  register it (same content, same version; changed, a new one) and run against it — so a run
  always records what really ran, and says whether the version is `new` or `existing`.
  `suwageval validate` reads it too, to check tool and event names (`--offline` skips that; an
  unreachable agent is a notice, not an error). An agent without the contract endpoint cannot be
  evaluated. The contract: [docs/scenario-format.md](docs/scenario-format.md).
- **The file format** (agent, scenarios, checks, shorthands, one complete example):
  [docs/scenario-format.md](docs/scenario-format.md). `suwageval validate` checks it, with no
  server.
- **The CLI is built for agents too:** arguments are paths, `-` reads stdin, `--json` prints a
  result to read by machine.
- **`Suwageval.Contracts`** holds the API's records — requests, responses, log items, checks —
  shared by the server and the CLI, so the file format and the API cannot drift apart.
- **No MCP server:** the files in the agent's repo are the source of truth, and the CLI keeps
  one way in for people, CI and Claude Code; no extra endpoint, no extra access question.

### Use it from Claude Code

The repository is also a [Claude Code](https://claude.com/claude-code) plugin marketplace. With
the plugin, Claude knows how to set the `suwageval` CLI up and drives it when you ask in your own
words. Install it at the Claude Code prompt:

```
/plugin install suwageval --marketplace Manomenu/suwalski-agent-eval
```

Answer `y` to add the marketplace, then pick a scope. To install it in two steps instead:

```
/plugin marketplace add Manomenu/suwalski-agent-eval
/plugin install suwageval@suwalski-agent-eval
```

Then ask in plain words, or use the commands:

| command | example |
| --- | --- |
| `/suwageval:scenario` | `/suwageval:scenario the agent refuses a refund after 14 days` |
| `/suwageval:fix` | `/suwageval:fix` — validate, then repair the scenarios the agent's change broke |
| `/suwageval:run` | `/suwageval:run tag regression` — push, run, and explain what failed |
| `/suwageval:versions` | `/suwageval:versions archive all but the last 3` — list versions, archive or restore them after you confirm |

The plugin needs the CLI: without it, Claude gives you the lines to install it. To update the
plugin later:

```
! claude plugin marketplace update suwalski-agent-eval
! claude plugin update suwageval@suwalski-agent-eval
```

and `/reload-plugins`. Claude Code keeps its own copy of the plugin, so it does not change until
you update it.

### Open questions

Decided so far (the agent under test over HTTP, tags, non-determinism, who calls the API):
`TODO.md`, "Decided for the slices to come". Still open:

- **The scenario format:** a JSON schema of our own, an import from existing traces
  (OpenTelemetry GenAI conventions, Agent Framework's serialized threads), or both?
- **The model providers** for the agent and the judge, and the cost limits of a run.

### Roadmap

**First goal (MVP):** from `suwalski-example-agent`, `suwageval validate`, `push` the agent
and a scenario, `run --tag regression --wait` against the running agent, and read the
result of deterministic checks (a tool called, an event, text) in the CLI. No judge, no
multi-turn simulation, no web views yet (the judge came right after: step 4).

1. **Contracts and `validate`** — done: `Suwageval.Contracts` (format v0) and the CLI's
   `validate`; the YAML files in the example agent.
2. **Agents, Scenarios (with tags), Checks** — done: the slices that store them (HTTP endpoints
   in `*Endpoints.cs`), and `suwageval push`.
3. **Invocation and Runs** — done: the agent under test over HTTP (contract v0: `/suwageval/turn` and `/suwageval/contract`, `docs/scenario-format.md`), the
   deterministic checks, and `suwageval run` (`--wait` exits 0 when every check passed its
   threshold, 1 when one did not, 2 when the run could not finish). The MVP is reached here.
4. **After the MVP** — the judge is done: a `judge` check, verdict by a model through Microsoft Agent Framework
   (`docs/scenario-format.md`, "The judge check"), with a daily live check of the model from the cluster. Still to come:
   multi-turn simulation, results and comparisons on the web.
   The Claude Code plugin over the CLI is done (see "Use it from Claude Code").

Microsoft Agent Framework came in with the judge, the first code that uses it (AGENTS.md, section 0), together
with its live test (`scripts/.internal/live-check.sh`) and the recorded answers the gate uses instead of a model.
The user simulator will use the same per-model client and pool of calls.

## Stack

- **Server:** .NET 10, ASP.NET Core minimal APIs, PostgreSQL through Npgsql and Dapper (SQL
  written by hand, numbered migrations), OpenAPI document the web app is typed from; Microsoft Agent Framework for the
  judge today, the user simulator after the MVP.
- **Web:** React 19, Mantine, Vite, TypeScript strict.
- **Around them:** PostgreSQL 17, a container per part, compose for the whole stack on a
  laptop, a Helm chart with a post-deploy smoke test for the cluster.

## Requirements

.NET SDK 10, `pnpm` (via corepack), `just`, `docker` with `docker compose` (on Fedora:
podman behind the `docker` command, with the Compose v2 plugin); for the full gate also
`helm`, `shellcheck` and `gitleaks`. After cloning: `just sync` (dependencies, the C#
formatter and the git hooks).

On Fedora (the owner's development machine):

```sh
sudo dnf install dotnet-sdk-10.0 nodejs podman podman-docker just helm ShellCheck
# docker compose: the official Compose v2 plugin (not podman-compose), talking to podman's socket
corepack enable --install-directory ~/.local/bin   # pnpm, in the version package.json pins
# gitleaks: the binary from https://github.com/gitleaks/gitleaks/releases, the version CI pins (8.30.1)
```

## Everyday commands

```sh
just                 # every recipe, grouped
cp .env.example .env # once: the judge's model (address and name), never committed
just db up           # local PostgreSQL on :5463
just server          # API on :6230 (OpenAPI document at /openapi/v1.json)
just web             # web app on :3230, proxies /api to the server
just up              # the whole stack in containers on :8093 — no cluster needed
just check           # the quality gate, exactly what CI runs
just cli validate DIR  # the suwageval CLI from source; `just cli-install` installs it globally
just tmux-dev        # tmux layout: server, web, example agent, spare shell, CLI shell (SUWAGEVAL_AGENT_DIR sets the agent folder)
just tmux-dev --restart  # the same, after killing the old session and everything running in it
just e2e             # browser tests against a real server and database
just secrets backup  # copy the local .env files into Bitwarden (restore on a new machine)
```

How the repo is organised and what every change must bring along: [AGENTS.md](AGENTS.md).

## License

MIT, © 2026 Piotr Gugnowski — see [LICENSE](LICENSE).
