---
description: Push the agent's Suwageval files, run the scenarios and explain what failed
argument-hint: "[tag <name>] — run only the scenarios with this tag"
allowed-tools: Bash(suwageval:*)
---

Push and run Suwageval scenarios, as the user describes it:

$ARGUMENTS

1. Run `suwageval push`. If it reports problems, show them and stop (`/suwageval:fix` repairs
   them); a server that does not answer is for the user to start, so say which address was tried
   (`SUWAGEVAL_URL`, default `http://localhost:3230`).
2. Run `suwageval run --wait --json`, with `--tag <name>` if the user named a tag. Exit code 0
   means every check passed, 1 some failed, 2 the run could not finish. 1 is a result, not an
   error; for 2 show `run.error` or the message and stop.
3. Summarise from the JSON: the agent version (`run.agentVersionId`), the totals
   (`checksPassed` / `checksFailed`), and for every failed check the scenario key, the check
   key and why (its samples' `message`). A scenario sample with an `error` is the agent
   failing, not a check; say which. Add the run's link:
   `{SUWAGEVAL_URL or http://localhost:3230}/runs/{run.id}`.
4. Offer to compare with the agent's previous run: `suwageval runs --json` lists the agent's
   newest runs, so the previous one is the next after `run.id`. If there is one, say how the
   totals changed, and, if the user wants more, compare the two as the skill's "Comparing runs"
   describes, ending with the link `{SUWAGEVAL_URL or http://localhost:3230}/compare?runs={previous},{run.id}`.
   Do not run `suwageval compare` without asking: it opens a browser.
5. Do not change files or checks to make a failure go away; if you suspect a bad check, say so.
