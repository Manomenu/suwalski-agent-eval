# AGENTS.md

Rules for anyone — human or AI agent — changing this repository. `CLAUDE.md` only points here; this file is the one to edit.
What the project is for: [README.md](README.md). Setup and everyday commands: [docs/development.md](docs/development.md). Not built yet: [TODO.md](TODO.md).

## Git: agents commit and push through suwgit

Agents commit and push only with [suwgit](https://github.com/Manomenu/suwgit) (`suwgit commit`, `suwgit push`), never with plain `git commit` or `git push`. The repository is registered in suwgit with:

| Setting | Value |
| --- | --- |
| language | `en` |
| autocommit | on |
| `--b2b` | `me` |
| `--project` | `AgenticAI` |

- When a change is finished and the gate is green, push it right away with `suwgit push`, not a bare `suwgit commit`.
- Without suwgit on the machine, or the repository not registered, do not fall back to plain git: leave the change in the working tree and say so.
- Everything that rewrites or discards history stays the owner's: no amend, reset, rebase, force-push or deleting branches. Merging and releasing are the owner's too.
- The main branch is `master`, never `main`: CI publishes images only from it.
- A change with a red gate is not committed: it stays in the working tree and the summary says why.
- One task, one branch. Unrelated refactors, renames, version bumps and fixes go to their own branch or into `TODO.md`; say so once when you reach for one.
- Agents never publish `Suwageval.AgentSdk`: a version on nuget.org cannot be deleted ([docs/release.md](docs/release.md)).

## Commands

- For people: a recipe in `justfile` (or `.just/`) with a one-line description; `just` lists them.
- For agents: a script in `scripts/.internal/`, or in `Suwageval.Gate/scripts/` when it checks quality; each answers `--help`. A recipe only calls the script.
- `Suwageval.Gate/scripts/check.sh --quick` while working, without the flag before calling a change done. `scripts/.internal/e2e.sh` is not in the gate: run it after changing anything a user clicks.

## Rules

- KISS and YAGNI first: the repo grows only when a real need shows up; one developer maintains it after hours. No dependency, setting or service "for later", no abstraction before the second use. When in doubt, leave it out, and say in the change what was left out and why.
- Plan a change by listing what it makes obsolete, and delete that first, in the same change.
- Correction or a new rule? Make the mistake impossible or loud first: a type → a lint → a test → a hook. Add a sentence here only for what none of these can check.
- A guard's message states the fix. Never weaken a guard to make a change pass: fix the change. A dependency against the layers means the code is in the wrong place.
- What the repo can prove, the gate proves: the server stays thin and does not validate its own committed files at start-up.
- A new feature brings its checks along: go through the list in [docs/development.md](docs/development.md) or say in the change why one does not apply.
- Anything the web can do, the CLI can do, and the plugin knows it: the same change adds the command, its line in the plugin's reference skill and its row in [docs/front-doors.md](docs/front-doors.md).
- A new service or setting reaches `compose.yaml` and the chart in the same change, and all three ways to run keep working ([docs/deployment.md](docs/deployment.md)).
- A new C# project or folder gets its place in `Suwageval.Gate/layers.json`; split a growing feature in a change of its own, moves only.
- Think before coding: state assumptions, name the readings of an unclear request instead of picking one silently, say when a simpler way exists.
- Surgical changes: every changed line traces to the task; mention other dead code instead of deleting it.
- Goal-driven: turn a task into a check that proves it (a failing test first for a bug) and loop until it passes.
- Several changes on one branch: build them all, then run the gate and the browser tests once, over everything.
- Larger, well-defined work (clear goal, known files, a check that says done) goes to a subagent on a cheaper model; design decisions and the review stay with the main agent.

## Specs

A spec's acceptance criteria live as guardrails: `Suwageval.Gate/scripts/criteria.sh` and `Suwageval.Gate/criteria.json` say what fits, its messages the fix. What it cannot see:

- Set ids with `vdoc.py ids`, never by hand: a dropped id is never reused, an id is never renumbered.
- A criterion no automated check can prove is a wrong criterion: ask the owner, never keep it unguarded.
- A criterion gets the lightest check that can fail for it: a plain test before a server test, a server test before a browser test.
- Before merging a spec'd feature, run `/suwagent:visual-guardrails`: `criteria.sh` sees that an id is named, not that the test would fail.

## Review

A person also reads the diff only when the change touches the always-careful list: login and permissions, payments, data migrations, the published contract or the SDK's public API, code without characterization tests. The list is revised after incidents.

## Where code goes

[docs/architecture.md](docs/architecture.md). Read it before adding a folder, a project or a feature. The web's practices: [docs/web.md](docs/web.md).
Docs say what and why, not where: a line that must change when the code moves does not belong in the docs.

## What no test can check

- Names are chosen word by word: domain words in the users' language (English here), software words in technical English. One word per thing, everywhere: server, web, database, README. Abbreviations only well-established and only inside a longer name.
- A comment says why, never what, as one line `// Why: <reason>` (`# Why:` in shell); if code needs a what comment, rename the code.
- Secrets never enter the repo: not in code, settings, tests or history. Local values live in user secrets or the gitignored `.env`; a real hit is removed and rotated, never allow-listed. Addresses of the owner's own infrastructure are treated the same.
- Personal rules (your tools, your habits) go in the gitignored `CLAUDE.local.md`, not here.
