---
description: List the agent's Suwageval versions, archive or restore them
argument-hint: "[what to archive, e.g. all but the last 3]"
allowed-tools: Bash(suwageval:*)
---

Work with the agent's versions, as the user describes it:

$ARGUMENTS

A version is never deleted — every run points at one. Archiving hides it from the web's default
views and the list; unarchiving brings it back.

1. Run `suwageval versions --archived --json` (the agent is the one in `suwageval/agent.yaml`).
   A server that does not answer is for the user to start; say which address was tried
   (`SUWAGEVAL_URL`, default `http://localhost:3230`). Each version has `id`, `label`,
   `createdAt` and `archivedAt` (null: active). The list is newest first.
2. With no request, show the versions (id, label, created, active or archived) and stop.
3. Otherwise work out the ids the request means (for example "all but the last 3": the active
   versions except the three newest). Do not guess: if the request is ambiguous, ask.
4. If the user named exact versions (ids or labels), run `suwageval versions archive <id>...`
   or `suwageval versions unarchive <id>...`. If you worked the ids out yourself, first show
   the list of ids (with labels and dates) you are about to change and ask the user to confirm;
   run the command only after they say yes.
5. Show `suwageval versions` afterwards and say how many versions changed. Exit code 2 is a
   server error or an unknown id: show the message.
