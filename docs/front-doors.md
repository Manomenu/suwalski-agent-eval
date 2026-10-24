# Front doors: the web, a CLI, and Claude Code through the CLI

The server has one interface, its HTTP API. Three users reach it:

| Who | Through | Does |
| --- | --- | --- |
| a person in the browser | the web app | browses agents, scenarios, runs, results, comparisons, archives versions and deletes an archived one with its runs (after typing its id), archives run groups and deletes an archived group with its runs (after typing its name), deletes a whole agent (after typing its name) |
| an agent's repository, by hand or in CI | the `suwageval` CLI (`Suwageval.Cli`) | `init` to set up `suwageval/agent.yaml` (and `scenarios/`) without touching what is already in it, `validate` the scenario files, `push` the agent, its scenarios and checks, `run --tag … --wait` (`--tag` repeatable: the scenarios with any of the tags) with an exit code for CI (`--ref <branch>` picks the branch to compare with, `--strict` fails on any failed check, not only on regressions; `rerun` and `recheck` take `--strict` too), `rerun <runId>` to repeat a run, `recheck <runId>` to judge a finished run's stored answers again with the current checks (`--dry-run` shows what would be evaluated), `cancel <runId>` to stop a queued or running one, `note <runId> <text>` (`--clear` removes it) to set the note of a run (`run --note` and `rerun --note` set it at the start), `results <runId>` to print a run's results (`--failed-only` for only what did not pass, `--json` for the records, `--open` to open the run on the web instead, `--open --print` to open its print dialog), `runs` to list the agent's newest runs that are in no group (`--version <id>` repeatable, `--reference` for reference runs only, `--tag <tag>` repeatable: the runs that ran the scenarios of any of the tags, `--group <name>` one group's runs, `--any-group` the runs in some group, `--all-runs` both; each line shows tags, branch, a reference mark and the judge model), `agents` to list the agents on the server (no `agent.yaml` needed), `scenarios` (`--tag`) and `scenario <key>` to read the agent's scenarios, their checks and revisions as stored, `trend` for the pass-rate trend run by run (`--json` is the chart's data), `unstable` to list the open checks that pass in some runs of a version and fail in others, `agent-errors` to list the open scenarios where the agent gave no answer (both take `--version <id>`, repeatable), `compare <runId> …` or `compare --group <name>` to print up to 20 runs of one agent side by side (a lettered column per run, the pass rate per tag, the checks per scenario with the failed ones) and, with `--baseline <runId>` (one of the runs), the changes of the others against it (pass rates, turn times, the judge, the checks that differ); `--json` for the record, `--open` to open the web page instead (`--open --print`: its print dialog, up to 7 runs), `run --group <name>` and `rerun --group <name>` to put a run in a group, `runs --groups` to list the groups (`--archived`: the archived ones too), `groups archive|unarchive <name>…` to hide or restore groups and `groups delete <name>` to delete an archived one with its runs (`--dry-run` shows what would go, `--yes` skips the question), `agent` to show what the server stores for the repository's agent, `versions` to list, archive and restore the agent's versions (those with a run outside a group, or none, unless `--any-group` lists only the versions whose every run is in a group, e.g. a sweep's variants, or `--all-versions` both), `versions delete <id>` to delete an archived one with its runs (`--dry-run` shows what would go, `--yes` skips the question), `versions show <id>` to print one version with its contract, `versions diff <a> <b>` to print what changed between two of them as a unified-style diff (`--json` for the record, `--open` for the web page), `delete-agent` to delete the repository's agent from the server with everything of it (`--dry-run`, `--yes` as for `versions delete`) |
| Claude Code | the Claude Code plugin in this repo (`claude-plugin/`) that drives the same CLI, like suwgit's | "build a scenario for this agent from this conversation", "run the regression tag on the new version", "why did scenario X fail" — writing the scenario as a file in the agent's repo, then `validate` and `push` |

- **Anything the web can do, the CLI — and so Claude Code through the plugin — can do too; not the
  other way round.** The CLI may do things the web cannot (push scenarios, say). A new action on
  the web arrives with its CLI command and the plugin's knowledge of it, in the same change; the gate checks it (`EveryEndpointHasACliCommandTests`: every route the server maps names the command that covers it, or says why it has none).
- **The scenario files live in the agent's repository**, in `suwageval/`: `agent.yaml` (the
  agent: `name`, `endpointUrl` and optionally Suwageval's own address `suwagevalUrl`; `suwageval init` writes it) and
  `scenarios/*.yaml` (a scenario with its checks inside; `user:` / `assistant:` shorthands for
  the log). Suwageval's address is the web app's, the API is under its `/api`: `--server`, else `SUWAGEVAL_URL`, else
  `suwagevalUrl` in `./suwageval/agent.yaml` (or the command's `--dir`), else `http://localhost:3230`
  (so on a laptop the CLI needs `just web` running too). The agent's address for `validate`, `run` and `rerun` is `--endpoint`, else
  `SUWAGEVAL_ENDPOINT_URL`, else `endpointUrl` in `agent.yaml` (`run` and `rerun`: else the registered / earlier run's endpoint).
- **The results are read on the web, and on stdout.** `suwageval run --wait` prints what was compared with, the regressions and new failing
  checks first, then the totals and the link to the run's page; it exits 0 unless a check regressed or is new and failing
  (1; `--strict`: unless any check failed; a run in a group has no regression comparison, so it exits as with `--strict`), 2 when the run could not finish; `suwageval results <runId>` prints the same results as text on stdout (failing checks with their reasons, turn times; `--failed-only` prints only what did not pass, as the web's "Hide passed checks" does; `--json`
  for the records, `--open` to open the page in the browser). A "Run again" button on the run's page (and `suwageval rerun <runId>`, same flags as
  `run`) starts a new run with the same agent, tags and endpoint, against the version the agent
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
- **A run has a note: why it was started** ("rebuilt the retrieval index", "trying model X"), free text of at most 500 characters. Set at the start (`run --note`, `rerun --note`; a rerun does not inherit the note of the run it repeats) and changeable at any time, also after the run finished: on the run page ("Add note" / "Edit note", saved in the background) or with `suwageval note <runId> <text>` (`--clear` removes it). Shown on the run page, in the agent's runs table, in `results`, `runs` and `compare` and in the comparison's column headers, and printed with them. It changes no verdict and no regression state.
- **`suwageval sweep` runs the variants for you.** The agent's repository says how to switch the agent to a variant in the
  `sweep:` section of `agent.yaml` (see scenario-format.md); `suwageval sweep [variant…] [--select] [--tag t]… [--group g]` then, for each
  variant (a file in `variantsDir`, named like the file without its extension; none named means all): runs `before`, runs
  `reconfigure` with `{variant}` filled in, waits until `GET {endpointUrl}/suwageval/contract` carries `label == <variant>`
  (at most `waitSeconds`), starts a run in the group (default `sweep-<yyyymmdd-hhmm>-<agent>`), waits for it and runs `after`.
  A variant that could not be run (a command exited non-zero, the label never showed up, the run failed) is a row of the
  summary and the sweep goes on; failing checks are results, not failures. The commands run from the current folder through `sh -c`
  (`cmd /c` on Windows); `restore` runs once at the end, `after` after every variant, also after Ctrl+C (which cancels the run
  being waited for). It exits 0, 2 when any variant failed or `restore` failed, 130 after Ctrl+C. It does not `push`: push first.
  The output ends with a row per variant and the link to the group's compare page.
  `--select` replaces naming the variants: it lists the files of `variantsDir` as a numbered list and reads the answer (`1,3`,
  `2-4` or `all`; asked again when it is not valid). It cannot be combined with named variants, and without a terminal
  (input redirected) it exits 2: name the variants then.
- **Runs are compared on one page.** `/compare?runs=<id>,<id>,…` and `/compare?group=<name>[&agent=<name>]` open it: a column per
  run, up to 20, absolute values. On an agent's page tick runs and press **Compare**, or open a group from its **Groups** section; `suwageval compare <runId> …` (or `--group <name>`) prints
  the same data on stdout (`--json` for the records, `--open` for the page) and `suwageval runs` lists the runs to pick from. Claude
  reads the printed table instead of opening a browser. A column header says what each line is: the run's label as the title
  (else "Unlabelled"), **Version** and **Run** links that say where they lead, the models, the data version, the tags the run was
  limited to (several are possible), its group (a link) and the date. The sections, in order: **What differs** (the contract
  fields that differ between the columns' versions: models and their parameters such as `reasoningEffort`, tools added, removed
  or changed, `otherConfig` lines, the retrieval data version; a prompt is only marked changed, with its diff against the
  baseline's version (else the first column's) on request; one version in every column is said so; folded at first, its header
  says how many rows it holds, the browser remembers it open, and paper always shows it whole); the summary (checks passed,
  samples answered, turn time);
  the pass rate per tag (a tag counts only for a run that ran all its scenarios, as they were when it was queued: a tag no run
  has a rate for is left out, and where a run has none the dash says how much ran, "Not every mediacom scenario ran (30 of
  44)"); and a row per scenario, listed by scenario or by tag, with a badge in words (all passed, some failed, all failed, no
  answer) and `3/4 checks`. A scenario row opens (keyboard-accessible, kept in the address as `&open=<key>`) into a matrix of
  checks by run with the reason each failed check failed, the samples without an answer and an "Open in run" link to that
  scenario's card on the run page. **Only rows that differ** hides the scenarios that came out the same in every column; it is on
  for runs named one by one and off for a group, which lists every scenario. **There is no baseline by default.** A baseline is
  chosen: "Use as baseline" on a column's header (kept in the address as `&baseline=<id>`; on the baseline itself the same button takes
  it back), `--baseline <runId>` on the CLI. It must be one of the compared runs, and two or more runs are needed. The page then
  adds the changes against it, for any selection (runs named one by one or a group): the pass rate per tag, the turn times and a
  warning when another judge model decided; the columns keep their order. Runs of different agents are refused.
- **Variants of an agent are compared side by side.** To compare models or configurations of one agent, restart the agent with
  each variant (its contract carries the model, so every variant is its own agent version; the contract's `label` names the
  variant) and start a run of each with the same group: `suwageval run --group sweep-2026-10-21 --wait`. A group name is
  lowercase kebab-case, at most 64 characters, and a run's group is set when it starts and not edited later. `suwageval sweep` does the loop (below); a script in
  the agent's repository can do it by hand and print the group's link, `/compare?group=<name>`. **A run in a
  group is an experiment**: it never becomes a reference run, its checks get no regression state (a re-check of it neither), it
  is left out of the pass-rate trend, the unstable checks and the agent errors (the chart says so), and `run --wait` exits as with
  `--strict` (1 when any check fails its threshold). The agent's runs table lists the runs in no group by default; its **Group**
  filter offers "No group", "Any group", "All runs" and each group by name, kept in the address (`?runs=any`, `?runs=all`,
  `?group=<name>`; `suwageval runs` has `--any-group`, `--all-runs`, `--group`). The **Groups** section works like Versions: select
  groups and archive them (hidden, restorable with "Show archived groups"); only an archived group can be deleted, with its runs,
  samples and verdicts, after its name is typed (`suwageval groups archive|unarchive|delete`). A run started in an archived group
  brings it back.
- **The run and comparison pages filter by tag.** Tag buttons above the tables (every tag of the view; a pressed button
  has `aria-pressed`) narrow the scenarios, or the checks of the comparison, to those with any selected tag, and the per-tag rows to
  the selected tags (the row of every check goes: there is no rate for "any of these tags"). Checks and Samples are counted again
  over what is left, from the per-scenario results the page already has, and say so (`Checks (procedure, serviceability)`); the turn
  time stays that of all scenarios and says so. The choice lives in the address, `&tag=procedure&tag=serviceability`, so a view can be
  shared; nothing selected shows everything. The CLI has no counterpart: it prints the whole run.
- **The run page and the comparison print as they are.** A "Print" button beside the filters opens the browser's print dialog (its
  preview is the page; "Save as PDF" is in it). The same view, changed only by `@media print` rules and an A4 landscape `@page`
  (`Suwageval.Web/src/print/print.css`): no controls, navigation or app shell; every sample of the run page and every scenario row of the
  comparison open; the filters stay applied, said in a line at the top (the selected tags, "passed checks hidden", the baseline). On
  paper the comparison's columns are narrower, so at most **7 runs** fit a page (`PRINT_MAX_RUNS`, derived from the printed column widths);
  with more the button is off and says to compare fewer runs, e.g. with the same baseline. CLI, for a person at the screen:
  `suwageval results <runId> --open --print` and `suwageval compare … --open --print` open the page with `print=1` in the address, so it
  opens the dialog once its data shows (`--print` without `--open`, or a comparison above 7 runs, exits 2). A PDF without a person is
  not built (TODO.md).
- **The run page can hide passed checks.** A switch "Hide passed checks" next to the tag filter (address: `&hide=passed`) hides the checks
  that passed, and a scenario whose every check passed and whose samples all answered; failed checks, checks not judged and samples with an
  agent error stay. A line says how many were hidden; the summary keeps the run's totals. CLI: `suwageval results <runId> --failed-only`.
- **The comparison with a baseline colours every cell by its change.** Without a baseline the best column of a row is marked green; with
  one, a cell better than the baseline's is green and a worse one red (per-tag rates, scenario cells, the checks of an opened scenario,
  the Summary's checks and samples); the baseline and equal cells stay neutral. Cells carry `data-change` and a text for screen readers.
- **What the web shows, the CLI prints.** `suwageval agents`, `agent`, `scenarios`, `scenario <key>`, `trend`, `runs`, `results`, `compare` and `versions diff` print on
  stdout (text, or `--json` for the records), so Claude reads them without opening a browser.
- **A scenario's definition opens on the run page.** The `{ }` button next to a scenario's key opens it as a scenario file
  (`docs/scenario-format.md`: key, tags, conversation in the log shorthands, `continuation` when it is not 1 turn, `fixtures`),
  highlighted, with a copy button. Its `samples` and `checks` are left out: the results show them. The server writes it from the
  scenario revision the run used, so a later push does not change what an old run shows (the tags are the ones the scenario
  carries now: they belong to no revision). The comments of the pushed file are not kept. `suwageval results <runId> --json`
  carries the same text as `definitionYaml`.
- **Unstable checks are marked.** A model does not answer the same way twice, so a check can pass in
  one run and fail in another of the same agent version: noise, not a regression. Such a check (among
  the newest 200 finished runs, by its pass threshold over the samples; an edited check is another
  check) gets a yellow wave icon next to its key on the run page and in the comparison, and the agent
  page lists the open ones in an "Unstable checks" card — nothing shows when none is open. A check is open while it
  is unstable in the **newest** version (among those the page's filters show; by default every version not
  archived) that ran it in at least 2 finished runs: a newer version that ran it once proves nothing yet, one that ran
  it twice or more without the problem closes it, and so does a newer revision of the check run at least twice. Archiving
  a version hides its rows. `suwageval unstable` prints them (`--version <id>`, repeatable, limits the versions considered); the cure is usually more `samples` (the agent's in `agent.yaml`, a scenario's or a check's; 3 by default).
- **Agent errors are listed apart.** A sample where the agent gave no answer (an HTTP error, a timeout, an unreadable
  answer) does not count against any check, so it would otherwise go unseen. The agent page's "Agent errors" card
  lists, per version and scenario, the samples without an answer out of the samples asked (`3/12`), over the same newest
  200 finished runs as the unstable checks (re-checks do not call the agent, so they are not counted), worst first, with
  the newest error. An error is open while the **newest** version (among those the filters show; by default every
  version not archived) that ran the scenario has agent errors in it: one clean run of a newer version closes it. The
  card shows nothing when none is open. `suwageval agent-errors` prints them (`--version <id>` as for `unstable`).
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

