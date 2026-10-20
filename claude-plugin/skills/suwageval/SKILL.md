---
name: suwageval
description: Set up and drive Suwageval, the evaluation server for LLM agents, through its suwageval CLI. Use when the user wants to evaluate or test an LLM agent with Suwageval, write or fix scenarios and checks, validate them, run them, or find out why a scenario failed, or asks to install or troubleshoot the suwageval CLI.
---

# Suwageval

Suwageval replays a recorded conversation against an LLM agent, lets the agent answer the last
message, and checks that answer. You drive it through the `suwageval` CLI, run through Bash.
Source and docs: https://github.com/Manomenu/suwalski-agent-eval

## First: is it installed?

```bash
command -v suwageval && suwageval --help
```

`command not found`: the CLI is not installed. Go to **Setup**.

## Setup

Needs the .NET 10 SDK, git and `just`. Give the user the lines to run, prefixed with `!` so they
run in this session's terminal:

```
! git clone https://github.com/Manomenu/suwalski-agent-eval ~/repos/suwalski-agent-eval
! cd ~/repos/suwalski-agent-eval && just cli-install
```

The CLI talks to the Suwageval server through the address of its **web app**; the API is under
its `/api`. The address is `--server` on a command, else `SUWAGEVAL_URL`, else `suwagevalUrl` in
`suwageval/agent.yaml`, else `http://localhost:3230` (a laptop running `just web` in the clone).
`validate` and `init` need no server.
`push` and `run` need the server, and `run` needs the agent itself running too.

## The workflow

- **Scenario files are code in the agent's repository**, in `suwageval/`: `agent.yaml` and
  `scenarios/*.yaml`. Git versions them together with the agent. Never edit them on the server:
  change the file, then `push`.
- **Read before writing.** Read the existing files in `suwageval/` and copy their style. The
  format (agent, scenarios, checks, shorthands, one complete example) is specified at
  https://raw.githubusercontent.com/Manomenu/suwalski-agent-eval/master/docs/scenario-format.md
  — fetch it when you write or change a scenario; do not rely on memory.
- **Never invent tool or event names.** Take them from the agent's contract: `GET
  {endpointUrl}/suwageval/contract` (the `endpointUrl` is in `agent.yaml`) lists its LLM agents (each with
  the tools it may call and their argument schemas) and its events, or read them from `suwageval validate` output.
- **After every change to the agent**, run `suwageval validate`: it lists the scenarios that no
  longer fit the agent's contract (a renamed tool, a removed event). Fix those files.
- A version is never written in a file: the running agent serves it, and `run` records what
  really ran.

## Commands

| want | command |
| --- | --- |
| set a repository up (no `suwageval/agent.yaml` yet, or a field to change) | `suwageval init [path] --name N --endpoint-url URL [--suwageval-url URL]`: give every value as an option, since there is no terminal to ask on; an existing file keeps its comments and other fields, and a repeat changes nothing |
| check the files and the names against the agent's contract | `suwageval validate [dir]` (`--json`, `--offline` to skip the agent, `--endpoint URL`) |
| send the agent, scenarios and checks to the server | `suwageval push [dir]` |
| run the scenarios and wait | `suwageval run --wait --json` (`--tag <tag>` for some scenarios only; `--ref <branch>` to compare with another branch's reference runs; `--strict` to exit 1 on any failed check) |
| run an earlier run again | `suwageval rerun <runId> --wait --json` (`--endpoint URL`, `--strict`): same agent, tag and endpoint, the version the agent serves now |
| judge a finished run's stored answers again with the current checks (no agent call) | `suwageval recheck <runId> --wait --json` (`--strict`); `--dry-run` first prints what would be evaluated again (exit 0 when no check changed: nothing is started). Use it after fixing a check; `rerun` is for when the agent changed |
| stop a queued or running run | `suwageval cancel <runId>`: samples in flight finish, no new one starts; a finished run stays as it is. Only for a run the user asked to stop |
| read a run's results (status, version, tag, branch, regressions, failing checks with reasons, turn times) | `suwageval results <runId>`: text on stdout; waits for a run still going. Prefer it to opening a browser |
| a run's results as JSON | `suwageval results <runId> --json` |
| open a run on the web, only when the user wants the page | `suwageval results <runId> --open` |
| list the agent's newest runs | `suwageval runs` (`--limit N`, `--json`; `--version <id>` repeatable for the runs of some versions, `--reference` for reference runs only): per run id, status, tag, version, checks passed, time, branch, a `reference` mark, the judge model(s) |
| find the agents on the server (the repository has no `agent.yaml`, or the user asks what exists) | `suwageval agents` (`--json`, `--server`): name, endpoint, creation time |
| list the agent's scenarios | `suwageval scenarios` (`--tag <tag>`, `--json`): key, revision, number of checks, tags |
| read one scenario as the server stores it | `suwageval scenario <key>` (`--json`): the conversation, every check with its definition, the revisions, the tags. Use it before changing or explaining a scenario that is not in the working tree |
| see how the pass rate moved over the runs | `suwageval trend` (`--json`): per finished run, oldest first, pass rate, median and p95 turn time and the rate per tag (only for a tag whose every scenario the run ran: a `--tag` run has no rate for another tag it ran in part) |
| list the checks that pass in some runs of a version and fail in others | `suwageval unstable` (`--json`; `--version <id>` repeatable): open ones only, i.e. unstable in the newest version (not archived by default) that ran the check at least twice; per check `scenarioKey`, `checkKey`, `passedRuns` of `runs`, the version; "No unstable checks." when none |
| list the scenarios where the agent gave no answer (HTTP error, timeout) | `suwageval agent-errors` (`--json`; `--version <id>` repeatable): open ones only, i.e. the newest version (not archived by default) that ran the scenario has errors; per version and scenario `errors` of `samples`, `newestErrorRunId`, `newestError`; "No agent errors." when none. Such samples do not count against checks: an agent that fails here is unreliable itself, not a check to fix |
| compare 2 to 5 runs of one agent | `suwageval compare <runId> <runId>...`: prints on stdout, no browser (the first run is the baseline): a header per run (version, tag, judge model, pass rate and turn times against the baseline), the pass rate per tag, the checks that differ with the trend (▲ better, ▼ worse), a `judge differs from baseline` warning when the judge model changed. `--json` prints the record. Exit 2 for an unknown run or runs of different agents |
| open that comparison on the web, only when the user wants the page | `suwageval compare <runId> <runId>... --open` |
| show what the server stores for the repository's agent | `suwageval agent` (`--json`): endpoint, number of versions, scenarios and checks, samples, regression settings |
| list the agent's versions | `suwageval versions` (`--archived` to include archived ones, `--json`) |
| archive or restore versions | `suwageval versions archive <versionId>...`, `suwageval versions unarchive <versionId>...` |
| delete an archived version with its runs, samples and verdicts (cannot be undone) | `suwageval versions delete <versionId> --dry-run` first, then `suwageval versions delete <versionId> --yes` once the user confirmed; exit 2 (409) for a version that is not archived or has a queued or running run |
| delete the agent of this repository from the server (a registration made by mistake) with its versions, scenarios, checks, runs, samples and verdicts (cannot be undone) | `suwageval delete-agent --dry-run` first, then `suwageval delete-agent --yes` once the user confirmed; exit 2 (409) while a run of the agent is queued or running |
| see what changed between two versions of the agent | `suwageval versions diff <versionA> <versionB>`: a unified-style diff of both contracts on stdout (`---`/`+++` headers, `@@` hunks, `+`/`-` lines, `+A −R lines`, `No changes.`); the older is the base; `--json` prints the record; exit 2 for an unknown version. Read it instead of opening a browser; `--open` opens the page for a person |
| print one version with its contract | `suwageval versions show <versionId>` (`--json`) |

A run's page is `{SUWAGEVAL_URL}/runs/{runId}`; the comparison of runs is `{SUWAGEVAL_URL}/compare?runs={id},{id},...`.
The comparison of two versions is `{SUWAGEVAL_URL}/agents/{agent}/versions/compare?a={id}&b={id}`.

### Exit codes

| code | `validate` | `run --wait`, `results --json` |
| --- | --- | --- |
| 0 | the files are valid | no regression, no new failing check and no agent error (with `--strict`: every check passed its threshold) |
| 1 | something is wrong | a check regressed, is new and failing, or is an agent error (with `--strict`: any check failed) |
| 2 | — | the run could not be started or finished |

`validate` exits 0 with a notice when the agent was unreachable: the names were then not
checked (`namesChecked: false` in `--json`). Say so; do not call the scenarios verified.

## Wiring an agent to the contract

When you add or change the agent's `GET /suwageval/contract` and `POST /suwageval/turn` (format:
the spec above, "The agent contract"), these decisions are yours:

- **List every LLM agent of the system** in `agents`, each with its own `model`, its own `config`
  (free text: its system prompt or whatever defines it) and only the `tools` it may call. Never glue
  several prompts into one agent: a change to one of them must show as that agent's change. What
  belongs to the system, not to one agent (retries, routing rules, timeouts), goes to `otherConfig`.
- **Report `agent` on every tool call** of a multi-agent system, the name from `agents`. Without it
  `by:` checks cannot pass and the results cannot say who acted.
- **`fixtures` only for what a scenario must fix explicitly** (for example the documents a retrieval
  is pinned to). The agent declares them in `context.fixturesSchema`; it never reads a hidden header
  or an environment switch instead.
- **Correlation headers** `Suwageval-Run-Id`, `Suwageval-Scenario`, `Suwageval-Sample` may go into the
  agent's traces and logs. They must never change what the agent does.
- **Report `sources`** (document ids) on the assistant messages whose answer is grounded, so `cited`
  can check them.
- **Every conversation starts from the same test data**: a fresh copy per turn, no state carried over.
- **Retrieval data version.** With `usesRetrieval: true` the contract needs `context.retrieval` with
  `dataVersion`: new data is a new agent version, so runs on different data are not compared as one.
  Compute it from the id of the index build (every rebuild gets a new one), or from a hash over
  (document id, content hash) of every document plus the pipeline settings (chunking, embedding model,
  index parameters). Never from a timestamp or "last updated" alone: a deletion, or a re-index with
  other chunking or embeddings, does not move it. For repeatable runs point the agent at a frozen
  snapshot of its data for evaluation, so `dataVersion` changes only when the snapshot is swapped on
  purpose. `description` is for people (what the data is), outside the hash.

## Choosing checks

- **`by:`** on `tool-called` / `tool-not-called` when the right tool from the wrong LLM agent would be
  a bug. A call that names no agent never satisfies `by`.
- **`result:`** on `tool-called` when an argument is only right in the light of what the tool
  returned (a refund for the id that `get_order` really returned).
- **`event-not-emitted`** is the negative of `event`; **`cited`** asserts that a grounded answer lists
  the given `sources`.
- **`{ $pattern: ... }`** inside `args`, `data` or `result` instead of exact equality for values a model
  writes loosely (case, spaces, abbreviations). Anchor it (`^...$`), `(?i)` for case. Exact equality for
  ids and numbers.
- **`durationMs`** of a turn is measured by Suwageval; the agent reports nothing for it, and there is
  no check on it, only the run's results.

## Scenarios of several steps

A conversation of several real steps is split into several scenarios. Each one holds the earlier
agent answers as predefined log items, and checks only its own step. Those answers are **recorded from
a real run of the agent**, never invented: take them from `suwageval results <runId> --json` or the
agent's own log. Never script fixed user messages after an answer you do not know: the next user
message must fit what the agent really said.

## Judge or deterministic check?

Prefer a deterministic check (`tool-called`, `tool-not-called`, `event`, `event-not-emitted`, `cited`, `text`) whenever one can
say it: exact, free, repeatable. Use `type: judge` with `criteria:` only for what needs reading —
tone, completeness, whether a refusal was explained. Write the criteria as one concrete, checkable
statement about the output ("states the 14-day refund period", not "is helpful"), one thing per
check, and narrow it with `where` (`turn: last`). A judge calls a model, so it can come back
**not judged** (`notJudged: true`, message `Not judged: …`): the model was unreachable or answered
unreadably. That is not the agent failing — say so, and run again before drawing conclusions. A
server without a judge model refuses a run with judge checks (409).

## Exact value, `$pattern` or `$judge`?

In `args` / `result` (of `tool-called`) and `data` (of `event`) prefer the **exact value** whenever you know it
(`order_id: A-17`): exact, free, repeatable. For a value written loosely but checkable by its form
(a zip code with stray spaces) use `{ $pattern: "(?i)^\s*12345\s*$" }` — free and repeatable too. For free text whose wording varies — a refund reason, a
search query, a message to a customer — use a matcher in place of the value:
`reason: { $judge: { description: "Says the item arrived damaged or broken", examples: ["kettle arrived broken", "damaged on delivery"] } }`.
Write a precise description (what the value must say) and 2–3 varied examples (different words, same
meaning); `description` and/or `examples`, nothing else inside `$judge`, and `$judge` is the only
property of its object. Literals around it still match exactly, and the model is asked only about calls
that passed them — but each candidate call costs a model request, and it can come back **not judged**
like a judge check. A server without a judge model refuses a run with `$judge` (409). `$judge` is not allowed in
`event-not-emitted`.

## Versions

Every change of the agent's contract makes a new version, so the list grows. Versions are never
deleted by default (every run points at one): they are **archived**, hidden by default and restorable. A new
run on a contract that matches an archived version unarchives it. Archive only what the user
asked for or confirmed: show the ids first. `/suwageval:versions` does this.

Only an archived version can be **deleted**, and that removes its runs, samples and verdicts for
good. Always run `suwageval versions delete <id> --dry-run` first, show the user what would go,
ask for explicit confirmation, and only then run it again with `--yes`.

The same goes for deleting the whole agent (`suwageval delete-agent`, for an agent pushed by mistake):
`--dry-run` first, show what would go, explicit confirmation, then `--yes`. Never delete the agent or a
version on your own initiative.

## Reading results

`run --wait --json` prints `{ run, scenarios }`. `run` has the agent, `agentVersionId`, `status`,
`error` and the totals (`checksPassed`, `checksFailed`). Each scenario has `key` and its
`checks`; a check has `key`, `passed`, `passRate`, `passThreshold` and per-sample `message` and
`evidence` — the message says why it failed. A judge check's message is the judge's reason; a sample with `notJudged: true` was not judged at all. A scenario sample with an `error` means the agent
itself failed on that turn, which is not a check failing: say which.

## Regressions

A run that finished has `results.regression` (what it was compared with, and counts) and each check a `regression`
(`state`: `Passed`, `Regression`, `New`, `AlreadyBroken`, `KnownFailure`, `Fixed`, `Unstable`, `AgentError`; `failingSince`, `lastPassedRun`,
`history`: the verdicts the state follows from, oldest first; the CLI prints them as `✓✓✓✗ → ✗`, the last mark being
this run). A regression is a check that passed steadily over the newest reference runs (clean tree, pushed commit,
the default branch or `regression.branches` of `agent.yaml`) and fails now. Report in this order: **regressions and new
failing checks** first (scenario, check, why; `lastPassedRun` is the run to compare with: `{SUWAGEVAL_URL}/compare?runs={lastPassedRun},{run.id}`),
then **already broken** (not caused by this run), then fixed and unstable ones; known failures only as a count. An
**agent error** failed only because the agent gave no answer (the CLI prints the agent's error): report it as the agent failing,
not the check; it is not counted in the check's history. Never change a check to hide a regression. When a behaviour
change was intended, the check is what is out of date: update it (`/suwageval:fix`), push, and run again; it starts a new
history, so it is *new* until it has run enough times.

## Unstable checks

A model does not answer the same way twice: a check can pass in one run and fail in another of the
**same** agent version. Before suggesting a fix for a failing check, run `suwageval unstable --json`
and look for it (by `scenarioKey`, `checkKey` and the run's version). When it is listed, say so first:
the failure may be noise, not a regression. Do not change the agent for it; suggest raising `samples`
of that scenario or that check (so the pass rate over samples decides) or loosening `passThreshold`, and leave the
choice to the user.

`samples` (an integer from 1 to 20, default 3) can be set on three levels, the most specific wins: a
check's, its scenario's, `agent.yaml`'s. The agent is called as many times as the scenario's most
demanding check needs, and each check is judged on its first N samples only (its pass rate and
`passThreshold` are over those). Raise it on a flaky scenario; put `samples: 1` on a check that does not
vary to keep runs fast.

## Comparing runs

To compare 2 to 5 runs of the same agent (for example the new run with the previous one): find
the runs with `suwageval runs --json` (newest first), fetch `suwageval results <runId> --json`
for each, and take the oldest as the baseline unless the user names another. Align checks by
scenario `key` and check `key`; a check **passes** when `passed` is true (its `passRate`
reached `passThreshold`). Report:

- **per tag**: the share of passed checks over the scenarios with that tag, per run, and the
  change against the baseline in percentage points (tags are the scenarios' `tags`, from
  `GET {SUWAGEVAL_URL}/api/agents/{agent}/scenarios`; there is no CLI for them, so fetch that);
  skip a tag for a run that did not run all of its scenarios, as `compare` and `trend` do;
- **regressions** (passed in the baseline, fails now, or the pass rate fell) and **improvements**
  (the opposite), each with scenario key, check key and the pass rates;
- checks present in some runs only: say so, they are not a regression;
- the agent versions of the runs (`run.agentVersionId`) and their models, so a change is not
  blamed on the wrong thing.

`suwageval compare <runId> <runId>...` does this alignment for you and prints it: read its output (or `--json`) instead of fetching every run; it flags a changed judge model, so a different verdict is not blamed on the agent. Finish with the comparison's link, `{SUWAGEVAL_URL}/compare?runs={id},{id},...` (the page `compare --open` opens; open it only if the user wants that).

## Rules

- **Push and run only when asked** (or when the command you were given says so): a push writes
  to the server.
- **Do not delete or weaken a check to make it pass.** If its intent no longer fits the agent,
  say so and let the user decide.
- **Do not change the agent to make a scenario pass** unless the user asked for that.
