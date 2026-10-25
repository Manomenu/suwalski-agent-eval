This repository's agent is tested and watched by Suwageval (from the suwageval plugin): `suwageval/agent.yaml`, scenarios in
`suwageval/scenarios/`, monitoring checks in `suwageval/monitoring-checks.yaml`, all driven through the `suwageval` CLI.

When the work touches how the agent behaves, go around this loop:

1. **What fails now.** `suwageval runs` and `suwageval results <run>`: the regressions, new failures and agent errors first. With
   monitoring on: `suwageval monitoring checks` (pass rates over 7 and 30 days) and
   `suwageval monitoring conversations --failed-check <key>` (the real conversations behind a falling rate).
2. **Capture it.** A failing real conversation becomes a scenario: `/suwageval:from-conversation` (cut at the message the agent got
   wrong; `--simulate` when what matters is how the conversation goes on). A new behaviour to pin: `/suwageval:scenario`.
3. **Run.** `suwageval validate`, then `/suwageval:run` (push, run, explain). Take the cheapest step that answers: only checks
   changed → `suwageval recheck <run>` (no agent call); one area → `run --tag <tag>`.
4. **Find why.** Read the failing scenario's conversation and the check's evidence. If the project has tracing or observability
   (its README says: Langfuse, OpenTelemetry, logs), look at the failing turn there too: the tool calls, retrievals and prompts.
5. **Fix the agent, not the test.** Change a check only when the intended behaviour changed, and say so to the user: a loosened
   check hides a regression. Run again until the regressions are gone.

`/suwageval:fix` repairs scenarios after an intended change of the agent; `/suwageval:update` brings this plugin to the newest
version when a flag or command seems missing.
