---
name: versions
description: List the agent's Suwageval versions, archive, restore or delete them
disable-model-invocation: true
argument-hint: "[what to archive, e.g. all but the last 3]"
allowed-tools:
  - Bash(suwageval:*)
---

Work with the agent's versions, as the user describes it:

$ARGUMENTS

Archiving hides a version from the web's default views and the list; unarchiving brings it back.
Only an archived version can be deleted, with all its runs, samples and verdicts — for good.

1. Run `suwageval versions --usage evaluation,groups,monitoring,unused,archived --json` (the agent is the one in
   `suwageval/agent.yaml`): every version, whatever it is used for; the default leaves out the versions used only in groups
   (a sweep's variants) and the archived ones, as the web does. A server that does not answer is for the user to start; say
   which address was tried (`SUWAGEVAL_URL`, default `http://localhost:3230`). Each version has `id`, `label`, `createdAt`,
   `archivedAt` (null: active), `runs` (outside a group), `groupRuns` and `monitoring` (per environment: conversations of 30
   days, the last one, `current`). The list is newest first.
2. With no request, show the versions (id, label, created, active or archived, runs, group runs, and where it answers
   conversations now) and stop.
3. Otherwise work out the ids the request means (for example "all but the last 3": the active
   versions except the three newest). Do not guess: if the request is ambiguous, ask. A version with group runs and no
   other runs belongs to an experiment: leave it out of a request about the agent's main line, and include it only when the
   user means the experiments too. Never archive a version that is `current` in monitoring without saying so.
4. If the user named exact versions (ids or labels), run `suwageval versions archive <id>...`
   or `suwageval versions unarchive <id>...`. If you worked the ids out yourself, first show
   the list of ids (with labels and dates) you are about to change and ask the user to confirm;
   run the command only after they say yes.
   To delete: the version must be archived first (archive it if the user asked for the deletion
   of an active one, after they confirm). Always run `suwageval versions delete <id> --dry-run`
   first and show the user what would be deleted (runs, samples, verdicts). Ask for explicit
   confirmation of that deletion; only after a clear yes run `suwageval versions delete <id> --yes`.
   Never use `--yes` before the dry run was shown and confirmed.
   If the user asks to delete the whole agent (pushed by mistake), the same rule applies to
   `suwageval delete-agent`: `--dry-run` first, show what would go, explicit confirmation, then `--yes`.
5. Show `suwageval versions` afterwards and say how many versions changed. Exit code 2 is a
   server error or an unknown id: show the message.
