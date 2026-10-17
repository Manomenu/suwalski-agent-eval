# Front doors: the web, a CLI, and Claude Code through the CLI

The server has one interface, its HTTP API. Three users reach it:

| Who | Through | Does |
| --- | --- | --- |
| a person in the browser | the web app | browses agents, scenarios, runs, results, comparisons, archives versions and deletes an archived one with its runs (after typing its id), deletes a whole agent (after typing its name) |
| an agent's repository, by hand or in CI | the `suwageval` CLI (`Suwageval.Cli`) | `init` to set up `suwageval/agent.yaml` (and `scenarios/`) without touching what is already in it, `validate` the scenario files, `push` the agent, its scenarios and checks, `run --tag … --wait` with an exit code for CI (`--ref <branch>` picks the branch to compare with, `--strict` fails on any failed check, not only on regressions; `rerun` and `recheck` take `--strict` too), `rerun <runId>` to repeat a run, `recheck <runId>` to judge a finished run's stored answers again with the current checks (`--dry-run` shows what would be evaluated), `cancel <runId>` to stop a queued or running one, `results <runId>` to open a run on the web, `runs` to list the agent's newest runs, `unstable` to list the checks that pass in some runs of a version and fail in others, `agent-errors` to list the scenarios where the agent gave no answer, `compare <runId> <runId> …` to open the comparison of 2 to 5 runs of one agent on the web, `versions` to list, archive and restore the agent's versions, `versions delete <id>` to delete an archived one with its runs (`--dry-run` shows what would go, `--yes` skips the question), `versions diff <a> <b>` to open what changed between two of them on the web, `delete-agent` to delete the repository's agent from the server with everything of it (`--dry-run`, `--yes` as for `versions delete`) |
| Claude Code | the Claude Code plugin in this repo (`claude-plugin/`) that drives the same CLI, like suwgit's | "build a scenario for this agent from this conversation", "run the regression tag on the new version", "why did scenario X fail" — writing the scenario as a file in the agent's repo, then `validate` and `push` |

- **Anything the web can do, the CLI — and so Claude Code through the plugin — can do too; not the
  other way round.** The CLI may do things the web cannot (push scenarios, say). A new action on
  the web arrives with its CLI command and the plugin's knowledge of it, in the same change.
- **The scenario files live in the agent's repository**, in `suwageval/`: `agent.yaml` (the
  agent: `name`, `endpointUrl` and optionally Suwageval's own address `suwagevalUrl`; `suwageval init` writes it) and
  `scenarios/*.yaml` (a scenario with its checks inside; `user:` / `assistant:` shorthands for
  the log). Suwageval's address is the web app's, the API is under its `/api`: `--server`, else `SUWAGEVAL_URL`, else
  `suwagevalUrl` in `./suwageval/agent.yaml` (or the command's `--dir`), else `http://localhost:3230`
  (so on a laptop the CLI needs `just web` running too).
- **The results are read on the web.** `suwageval run --wait` prints what was compared with, the regressions and new failing
  checks first, then the totals and the link to the run's page; it exits 0 unless a check regressed or is new and failing
  (1; `--strict`: unless any check failed), 2 when the run could not finish; `suwageval results <runId>` opens that page in
  the browser. A "Run again" button on the run's page (and `suwageval rerun <runId>`, same flags as
  `run`) starts a new run with the same agent, tag and endpoint, against the version the agent
  serves now. A "Re-check" button beside it (and `suwageval recheck <runId>`) judges the run's *stored answers* again with the
  *current* checks, without calling the agent: use it after fixing a check (a `$pattern`, a judge's criteria) to see the
  effect on the same answers, so any difference comes from the checks alone. It first shows what would be evaluated again
  (the checks that changed, were added or removed, how many a model judges, the scenarios left out) and is refused when
  nothing changed. A check that did not change keeps its verdict from the run, so no judge model is asked again; a scenario
  edited or deleted since is skipped, its answers belong to another conversation. The re-check is a run of its own ("Re-check
  of …" on its page, a badge in the run list) whose turn times are the source's, copied. On the pass-rate trend a re-check
  is no point of its own: its run's point shows the results of its latest re-check. A "Cancel run" button on the page of a queued or running run (and `suwageval cancel <runId>`)
  stops it: samples already in flight finish, no new one starts, and the run ends as cancelled. Ctrl+C
  while `run --wait` (or `rerun --wait`, `recheck --wait`) waits asks whether to cancel the run on the server too; answered
  no, the run goes on and the exit code is 130. Without a terminal to ask on (a CI job being stopped) the
  run is cancelled, since nobody would read its results. For machines, `--json` on either prints the results (and opens nothing).
- **Runs are compared on the web.** On an agent's page, tick 2 to 5 runs (the model filter narrows
  the list) and press Compare: the page shows each run's pass rate per scenario tag and, per
  check, where the runs differ from the baseline (the oldest selected run, or the one you pick).
  `suwageval compare <runId> <runId> …` opens the same page; `suwageval runs` lists the runs to
  pick from. Claude compares through `results --json` of each run.
- **Unstable checks are marked.** A model does not answer the same way twice, so a check can pass in
  one run and fail in another of the same agent version: noise, not a regression. Such a check (among
  the newest 200 finished runs, by its pass threshold over the samples; an edited check is another
  check) gets a yellow wave icon next to its key on the run page and in the comparison, and the agent
  page lists the worst ones in an "Unstable checks" card — nothing shows when all checks are stable.
  `suwageval unstable` prints them; the cure is usually more `samples` (the agent's in `agent.yaml`, a scenario's or a check's; 3 by default).
- **Agent errors are listed apart.** A sample where the agent gave no answer (an HTTP error, a timeout, an unreadable
  answer) does not count against any check, so it would otherwise go unseen. The agent page's "Agent errors" card
  lists, per version and scenario, the samples without an answer out of the samples asked (`3/12`), over the same newest
  200 finished runs as the unstable checks (re-checks do not call the agent, so they are not counted), worst first, with
  the newest error; it follows the page's filters and shows nothing when there are no errors. `suwageval agent-errors` prints them.
- **A version is never written in a file: the running agent serves it.** `GET
  {endpointUrl}/suwageval/contract` returns its LLM agents (each with model, config and tools, with argument
  schemas), other system-wide config, the events (with data schemas), and a label. `suwageval run` makes the server read it,
  register it (same content, same version; changed, a new one) and run against it — so a run
  always records what really ran, and says whether the version is `new` or `existing`.
  `suwageval validate` reads it too, to check tool and event names (`--offline` skips that; an
  unreachable agent is a notice, not an error). An agent without the contract endpoint cannot be
  evaluated. The contract: [scenario-format.md](scenario-format.md).
- **The file format** (agent, scenarios, checks, shorthands, one complete example):
  [scenario-format.md](scenario-format.md). `suwageval validate` checks it, with no
  server.
- **The CLI is built for agents too:** arguments are paths, `-` reads stdin, `--json` prints a
  result to read by machine.
- **`Suwageval.Contracts`** holds the API's records — requests, responses, log items, checks —
  shared by the server and the CLI, so the file format and the API cannot drift apart.
- **No MCP server:** the files in the agent's repo are the source of truth, and the CLI keeps
  one way in for people, CI and Claude Code; no extra endpoint, no extra access question.


## Use it from Claude Code

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
| `/suwageval:run` | `/suwageval:run tag regression` — push, run, and explain what failed; then offers comparing with the agent's previous run |
| `/suwageval:versions` | `/suwageval:versions archive all but the last 3` — list versions, archive, restore or delete them after you confirm |

The plugin needs the CLI: without it, Claude gives you the lines to install it. To update the
plugin later:

```
! claude plugin marketplace update suwalski-agent-eval
! claude plugin update suwageval@suwalski-agent-eval
```

and `/reload-plugins`. Claude Code keeps its own copy of the plugin, so it does not change until
you update it.

