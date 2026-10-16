---
description: After the agent changed, validate the Suwageval scenarios and fix those that no longer fit
argument-hint: "[dir] — the suwageval folder, default ./suwageval"
allowed-tools: Bash(suwageval:*), Bash(curl:*), WebFetch
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
4. Report what changed, file by file. List separately what needs the user's decision — a check
   whose intent no longer makes sense after the change, a removed tool with no successor — and
   leave it in place rather than deleting or weakening it. Do not `push` or `run`.
