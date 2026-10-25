---
name: fix
description: After the agent changed, validate the Suwageval scenarios and fix those that no longer fit
when_to_use: The agent under test changed (prompt, tools, events, contract) and `suwageval validate` fails or scenarios no longer fit.
argument-hint: "[dir] — the suwageval folder, default ./suwageval"
allowed-tools:
  - Bash(suwageval:*)
  - Bash(curl:*)
  - WebFetch
---

The agent changed; bring its Suwageval scenarios back in line. Folder, if given: $ARGUMENTS

1. Run `suwageval validate $ARGUMENTS --json`. Note `namesChecked`: when it is `false` the
   agent was unreachable and its tool and event names were not checked; say so.
2. For each problem (file, path, message), read the file and fix it: take the right tool or
   event name from the agent's contract (`curl` of `{endpointUrl}/suwageval/contract`) or from
   the message. Never invent a name. The contract lists several LLM agents, each with its own tools:
   a tool call's `agent` and a check's `by` must name an agent that has that tool; `fixtures` need
   `context.fixturesSchema`; `cited`, `event-not-emitted`, `result` and `$pattern` are available. The format is specified at
   https://raw.githubusercontent.com/Manomenu/suwalski-agent-eval/master/docs/scenario-format.md
3. Run `suwageval validate` again; repeat until it is clean.
4. When a *check* was fixed (a `$pattern`, a judge's criteria, a threshold) and a run showed the problem, do not
   run the agent again: once the fixed check is pushed (`suwageval push`, only when the user asked for it), `suwageval recheck <runId> --dry-run` on that run to confirm
   the fix is seen as a change (a check that did not change is not re-evaluated; exit 0 with nothing to do means the
   push did not change it), then `suwageval recheck <runId> --wait --json`. It judges the same stored answers with the
   current checks, so any difference from the original run comes from the checks alone: compare the two runs
   (`suwageval results <runId> --json` for each). A new `run` is for when the agent changed.
5. Report what changed, file by file. List separately what needs the user's decision — a check
   whose intent no longer makes sense after the change, a removed tool with no successor — and
   leave it in place rather than deleting or weakening it. Do not `run`.
