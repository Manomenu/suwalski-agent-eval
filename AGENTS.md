# AGENTS.md

Rules for anyone — human or AI agent — changing this repository. `CLAUDE.md` only points here; this file is the one to edit.
What the project is for: [README.md](README.md). Setup and everyday commands: [docs/development.md](docs/development.md). Not built yet: [TODO.md](TODO.md).

Read @AGENTS.shared.md first: the rules every repository of the stack shares. This file adds this repository's own and wins where it says otherwise.

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
- No agent deletes a branch, merged or not: that stays the owner's.
- CI publishes images only from `master`.
- `Suwageval.AgentSdk` is released through CI with `just agent-publish <version>`: a version on nuget.org cannot be deleted, only unlisted ([docs/release.md](docs/release.md)).

## Commands

- For people: recipes also live in a module of the whole repository beside `justfile` (`tmux-dev.just`, `secrets.just`).
- For agents: a script in `scripts/.internal/`, or in `Suwageval.Gate/scripts/` when it checks quality; a recipe calls it. The gate is
  `suwgate` (`Suwageval.Web/node_modules/.bin/suwgate`), configured in `suwgate.yml`; `suwgate dev` runs the local database and server.
- `suwgate step e2e -- --headed` after changing anything a user clicks; `just check` (the full `suwgate check`, with this checkout's databases and the browser tests) before calling a change done.

## Rules

- No dependency, setting or service "for later", no abstraction before the second use.
- A dependency against the layers means the code is in the wrong place.
- What the repo can prove, the gate proves: the server stays thin and does not validate its own committed files at start-up.
- A new feature brings its checks along: go through the list in [docs/development.md](docs/development.md) or say in the change why one does not apply.
- Anything the web can do, the CLI can do, and the plugin knows it: the same change adds the command, its line in the plugin's reference skill and its row in [docs/front-doors.md](docs/front-doors.md).
- A new service or setting reaches `compose.yaml` and the chart in the same change, and all three ways to run keep working ([docs/deployment.md](docs/deployment.md)).
- A new C# project or folder gets its place in `layers.json` (a gate file: give the owner the line); split a growing feature in a change of its own, moves only.
- Think before coding: state assumptions, name the readings of an unclear request instead of picking one silently, say when a simpler way exists.
- Surgical changes: every changed line traces to the task; mention other dead code instead of deleting it.
- Goal-driven: turn a task into a check that proves it (a failing test first for a bug) and loop until it passes.
- Larger, well-defined work (clear goal, known files, a check that says done) goes to a subagent on a cheaper model; design decisions and the review stay with the main agent.
- The null-forgiving `!` is allowed in tests.

## Specs

A criterion gets a plain test before a server test, a server test before a browser test. The kinds come with the profiles of `suwgate.yml`, this repository's own are in `criteria.json`.

## Review

The always-careful list: login and permissions, payments, data migrations, the published contract or the SDK's public API, code without characterization tests.

## Where code goes

The web's practices: [docs/web.md](docs/web.md).

## What no test can check

- Names: the users' language here is English. One word per thing: server, web, database, README. Abbreviations only well-established and only inside a longer name.
- Addresses of the owner's own infrastructure are treated like secrets.
