# Shared rules

The rules every repository of the owner's stack shares. suwgate ships this file, and its `agents-shared` guard keeps this copy
identical to it: change a shared rule in suwgate, never in a copy; `suwgate agents-shared` writes the current copy. The
repository's own `AGENTS.md` adds what is only its own, and wins where it says otherwise.

## Working

- For people: a recipe in `justfile` with a one-line description; `just` lists them. A recipe only calls a script or a command,
  and a script answers `--help` with a usage text printed by the script, never with header comments.
- While working, `suwgate check --quick`, and `suwgate step <step> [-- <tool args>]` runs one step (`suwgate step` lists them);
  `suwgate check` before calling a change done. `AGENTS.md` says how this repository runs suwgate.
- Test last: finish every change of the task, then run the full gate once and push once, so CI runs once. After a red run, fix all
  it shows, rerun only the tests that failed, and the full gate once when they pass. Quick checks of the part you change (its unit
  tests, the type checker, one step) are fine while building.
- KISS and YAGNI first: a repository grows only when a real need shows up; one developer maintains it after hours. When in doubt,
  leave it out, and say in the change what was left out and why.
- Plan a change by listing what it makes obsolete, and delete that first, in the same change.
- Correction or a new rule? Make the mistake impossible or loud first: a type, a lint, a test, a guard, a hook. Add a sentence to
  `AGENTS.md` only for what none of these can check.
- A guard's message states the fix. Never weaken a guard to make a change pass: fix the change.
- Mutation tests run only when a person asks for them: an agent neither runs nor proposes them on its own.

## Git

- The main branch is `master`, never `main`.
- Work on a feature branch, never on `master`. Agents do not rewrite or discard history: no amend, reset, rebase, stash,
  force-push or deleting an unmerged branch.
- An agent may merge a pull request whose CI is green, and release. Only CI publishes: nobody logs in to a registry.
- A branch merged into `master` is deleted right after the merge: local, remote and its worktree, with `git branch -d`, which
  refuses unmerged work. Needed again? Start a new one from `master`.
- A change with a red gate is not committed: it stays in the working tree and the summary says why.
- One task, one branch. Unrelated refactors, renames, version bumps and fixes go to their own branch or into `TODO.md`; say so
  once when you reach for one, and put it in if the owner confirms.
- Personal rules (how you commit, your tools) go in the gitignored `CLAUDE.local.md`, not in a shared file.

## Specs

A spec's acceptance criteria live as guardrails: the `criteria` step of the gate says what fits, its messages the fix. What it
cannot see:

- Set ids with `vdoc.py ids`, never by hand: a dropped id is never reused, an id is never renumbered.
- A criterion no automated check can prove is a wrong criterion: ask the owner, never keep it unguarded.
- A criterion gets the lightest check that can fail for it: a plain unit test before anything that needs a server, a browser or a
  screenshot, which cover only what nothing lighter can.
- A criterion that promises something for every member of a set that grows (every event, role, page, component) is guarded by
  structure, not by examples: a type derived from the set's source, so a new member is a compile error until it is handled, or a
  test that walks every member, from the source or the rendered structure. A test that names the members it checks lets the next
  one through.
- A spec written before its feature is built is a draft in docs/draft/spec/, which the gate does not read; it moves to
  docs/visual/spec/ when its feature is built, together with the tests that guard its criteria.
- No test promises speed, in milliseconds or in steps counted instead of them: a timing guard catches the clock, not a count, so
  a speed promise is dropped from its spec.
- Before merging a spec'd feature, run `/suwagent:visual-guardrails`: the `criteria` step sees that an id is named, not that the
  test would fail.

## Review

A person also reads the diff only when the change touches the always-careful list that `AGENTS.md` names. The list is revised
after incidents.

## Docs, names and secrets

- `docs/architecture.md` says where code goes: read it before adding a folder, a project or a feature.
- Docs say what and why, not where: a line that must change when the code moves does not belong in the docs.
- Names are chosen word by word: domain words in the users' language, software words in technical English. One word per thing,
  everywhere.
- Secrets never enter the repo: not in code, settings, tests or history. Local values live in gitignored files (user secrets,
  `.env`); a real hit is removed and rotated, never allow-listed.

## No comments

No comments in project files: agents mostly use comments to excuse bad code and comments rot, so names, small functions, types and
tests say what a comment would; if code is unclear without a comment, fix the code.

- Covered: every code and config file, doc comments (`///`, JSDoc), commented-out code and task markers included. Not covered:
  Markdown, `docs/` and data strings (UI texts, messages). Lockfiles, dependencies and build output are not scanned.
- Allowed: only a comment a tool reads (a shebang, a recipe's line for `just --list`, a linter directive with its reason, a
  criterion id where the `criteria` step reads it). suwgate's comment guards hold the exact list, and their messages say what fits;
  nothing is judged by eye.
- `#pragma warning disable`, `@ts-ignore`, `@ts-nocheck`, `eslint-disable` and the null-forgiving `!` are banned outright; no
  comment excuses them.
- A reason that cannot live in code goes into a test, the spec, `AGENTS.md` or `docs/` as a sentence. A new format that needs a
  comment to work goes into suwgate's comment guards in the same change.
