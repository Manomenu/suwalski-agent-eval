---
description: Push the agent's Suwageval files, run the scenarios and explain what failed
argument-hint: "[tag <name>] [group <name>] — only the scenarios with this tag; the run in a named experiment group"
allowed-tools: Bash(suwageval:*)
---

Push and run Suwageval scenarios, as the user describes it:

$ARGUMENTS

1. Run `suwageval push`. If it reports problems, show them and stop (`/suwageval:fix` repairs
   them); a server that does not answer is for the user to start, so say which address was tried
   (`SUWAGEVAL_URL`, default `http://localhost:3230`).
2. If the user asks to run again or repeat a run ("the last run"), find it with `suwageval runs --json`
   (newest first) and run `suwageval rerun <id> --wait --json --note "<why>"` instead: it keeps that run's tags and
   endpoint and uses the version the agent serves now. Summarise as for a run, and offer the comparison
   with the original run (`{SUWAGEVAL_URL or http://localhost:3230}/compare?runs={original},{run.id}&baseline={original}`).
   Otherwise run `suwageval run --wait --json --note "<why>"`, with `--tag <name>` (repeat it for several) if the user named tags, and
   `--group <name>` when the run is an experiment (a model or a setting being tried): name the group as the skill's **Names** says, and give the group a note (`suwageval note --group <name> "<text>"` after the first run, or `sweep --note`) as **Names** says for the group's note. The note is one sentence on why the run is started, as **Names** says for `--note`; leave it out when the reason is not known. Exit code 0
   means no regression and no new failing check (known, already broken and unstable failures leave it 0;
   `--strict` makes any failed check exit 1), 1 a check regressed, is new and failing, or is an agent error, 2 the run could
   not finish (or was cancelled). A run with `status` `Interrupted` is not the agent's fault: the server stopped during it (a restart or a deploy), the samples stored so far are kept; say so and offer `suwageval rerun <id> --wait --json`. 1 is a result, not an error; for 2 show `run.error` or the message and stop. 130 means the waiting was
   interrupted and the run goes on: say so, with its id.
   If the user asks to stop a run, cancel it with `suwageval cancel <id>` (find the id with
   `suwageval runs --json`): only the run they named, never one on your own initiative.
3. Summarise from the JSON: the agent version (`run.agentVersionId`), what the run was compared with
   (`regression.comparedWith`), the totals (`checksPassed` / `checksFailed`), and, from each check's
   `regression.state`, in this order: **regressions, new failing checks and agent errors** first (scenario key, check
   key, why: its samples' `message`; for a regression the link
   `{SUWAGEVAL_URL or http://localhost:3230}/compare?runs={lastPassedRun},{run.id}&baseline={lastPassedRun}`), then **already broken**
   (not caused by this run, `failingSince`), then fixed and unstable ones; known failures only as a count.
   Without `regression` in the results, list every failed check as before. A scenario sample with an `error` is the agent
   failing, not a check; say which. Add the run's link:
   `{SUWAGEVAL_URL or http://localhost:3230}/runs/{run.id}`.
4. Offer to compare with the agent's previous run: `suwageval runs --json` lists the agent's
   newest runs, so the previous one is the next after `run.id`. If there is one, say how the
   totals changed, and, if the user wants more, run `suwageval compare {previous} {run.id} --baseline {previous}` and
   report it as the skill's "Comparing runs" describes, ending with the link
   `{SUWAGEVAL_URL or http://localhost:3230}/compare?runs={previous},{run.id}&baseline={previous}`.
   Do not run `suwageval compare --open` without asking: it opens a browser.
5. Do not change files or checks to make a failure go away, a regression above all; if you suspect a bad check, say so.
   When the user says the behaviour change was intended, the check is what is out of date: update it (`/suwageval:fix`)
   instead of treating the regression as noise.
