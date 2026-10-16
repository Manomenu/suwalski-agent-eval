---
description: Write a new Suwageval scenario as a file in suwageval/scenarios/ and validate it
argument-hint: "<what the scenario should check> — or nothing, to build it from this conversation"
allowed-tools: Bash(suwageval:*), Bash(curl:*), WebFetch
---

Write a Suwageval scenario for:

$ARGUMENTS

If nothing is given above, build it from the conversation so far: the messages are the log, the
last user message is the one the agent answers next.

1. Read `suwageval/agent.yaml` and the existing files in `suwageval/scenarios/`; copy their
   style and key naming. Fetch the format spec,
   https://raw.githubusercontent.com/Manomenu/suwalski-agent-eval/master/docs/scenario-format.md,
   if you have not read it in this conversation.
2. Get the agent's real tool and event names from its contract (`curl` of
   `{endpointUrl}/suwageval/contract`, `endpointUrl` from `agent.yaml`). Never invent one. If
   the agent is not reachable, use `suwageval validate --offline` and say the names are unchecked.
   Prefer deterministic checks. Add a `type: judge` check (`criteria:` — one concrete, checkable
   statement, one thing per check, `where` to narrow it) only for what needs reading, like tone or
   whether a refusal was explained. In `args` / `data` use the exact value when it is known; for
   free-text arguments (a reason, a search query, a message to a customer) use
   `{ $judge: { description: "…", examples: ["…", "…"] } }` — a precise description and 2–3 varied
   examples; it costs a model call per candidate, so use it sparingly.
3. Write the scenario as one new file in `suwageval/scenarios/`, named after what it checks
   (`refund-happy-path.yaml`), with the checks inside. Use the `user:` / `assistant:`
   shorthands where they fit.
4. Run `suwageval validate`; fix the file and run it again until it is valid.
5. Report the file and what each check asserts. Do not `push` or `run` unless the user asked.
