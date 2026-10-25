---
description: Turn a monitored conversation (a real one the deployed agent had) into a scenario file, after you confirm
argument-hint: "<conversation id> — or nothing, to pick from the newest conversations"
allowed-tools: Bash(suwageval:*)
---

Turn a monitored conversation into a Suwageval scenario:

$ARGUMENTS

1. Read `features` in `suwageval/agent.yaml`. Without `monitoring` there are no conversations: say so and propose switching it on
   (then `suwageval push`) instead of going on.
2. No id given: run `suwageval monitoring conversations` and ask which one (a closed one, ideally one where the agent went wrong).
3. Decide with the user where the copy ends: the whole conversation (`suwageval scenario from-conversation <id>`), or up to a message,
   included (`--up-to-user N` / `--up-to-agent N`: the N-th user or agent message; read the conversation with `suwageval monitoring
   conversation <id>` to pick it) — usually the user message the agent answered wrongly, so that the scenario asks the agent that again.
   When what matters is how the agent copes as the conversation goes on (not one answer), propose `--simulate` with the cut: the rest
   is played by a simulated user a model describes from the whole conversation, for as many turns as the real one went on
   (`--turns N` to change it). Review that description with the user: it may need a fact corrected. A notice on stderr means no
   model wrote it: then write it together from the user's messages it lists.
   Run it and show the draft. The comments at the end are the monitoring checks the conversation failed, with their definitions:
   what went wrong, not checks of the scenario yet.
4. With the user, write the scenario's checks: what the agent must do in answer to the copied conversation, starting from the failed
   monitoring checks (a `judge` criteria can often be kept as it is; drop `description` where the scenario does not need it) and the
   scenario command's rules for choosing checks. Add the `fixtures` the agent needs, if its contract declares
   `context.fixturesSchema`: the conversation ran on real data. Rename the key if a better one says what it checks.
5. Ask before writing. Then `suwageval scenario from-conversation <id> --write` with the same options (it never overwrites a file; a
   simulated rest is described again, so keep the reviewed description), apply the decided edits to
   the written file, and run `suwageval validate` until it is valid.
6. Report the file and what each check asserts. Do not `push` or `run` unless the user asked.
