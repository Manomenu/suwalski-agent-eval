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
3. Run `suwageval scenario from-conversation <id>` and show the draft. It takes the conversation as it is: every turn before the last is
   the history, and the agent answers the last user message. The checks at the end are drafts, as comments, made from what the agent
   did: they are not the expected answer (the agent may have been wrong; that is often why the conversation was picked).
4. With the user, decide the checks: uncomment the ones that state what the agent must do, fix their values, delete the rest, add what
   is missing (the scenario command's rules for choosing checks apply). Add the `fixtures` the agent needs, if its contract declares
   `context.fixturesSchema`: the conversation ran on real data. Rename the key if a better one says what it checks.
5. Ask before writing. Then `suwageval scenario from-conversation <id> --write` (it never overwrites a file), apply the decided edits to
   the written file, and run `suwageval validate` until it is valid.
6. Report the file and what each check asserts. Do not `push` or `run` unless the user asked.
