# AGENTS.md

Rules for anyone — human or AI agent — changing this repository. `CLAUDE.md` only points
here; this file is the one to edit. What the project is for: [README.md](README.md).

## Template upstream: good practices flow back to solid-app-tpl

This repository was created from **solid-app-tpl** (`~/repos/solid-app-tpl/`,
https://github.com/Manomenu/solid-app-tpl) — the template every new app starts from — and
translated from its stack (FastAPI, uv, ruff, pyright) to .NET. The rules, the layout, the
gates, the container stack and the chart are the template's; only the tools that run them
differ (section 3 maps one onto the other).

**When a task introduces a good practice of running a repository** — a new gate step, a
convention for layout or naming, a script, a CI job, a rule in this file, a fix to the
tooling — **and `~/repos/solid-app-tpl/` exists on this machine, bring it to the template
too, in the same task**, as long as it is not specific to .NET:

- in its generic form: `myapp` names, no feature or domain code, no secrets, no addresses;
- with the template's gate green afterwards (`./scripts/.internal/check.sh` there), and
  `init-project.sh` updated when it rewrites the new files;
- and say in the summary what went to the template. Features, domain rules and .NET-only
  tooling stay here; only how the repo is *run* travels.

## Git: agents commit and push through suwgit

**Agents may commit and push — only with [suwgit](https://github.com/Manomenu/suwgit)**
(`suwgit commit`, `suwgit push`), never with plain `git commit` / `git push`. This repository
is registered in suwgit with:

| Setting | Value |
| --- | --- |
| language | `en` |
| autocommit | on |
| `--b2b` | `me` |
| `--project` | `AgenticAI` |

- When a change is finished and the gate (`./scripts/.internal/check.sh`) is green, **push it
  right away** with `suwgit push` (it commits and pushes) — not a bare `suwgit commit`.
- Everything that rewrites or discards history stays the owner's: no amend, reset, rebase,
  force-push or deleting branches.
- Without suwgit on the machine (or the repository not registered), do not fall back to plain
  git: leave the change in the working tree and say so.

**The main branch is `master`**, never `main`: CI publishes images only from `master`
(`.github/workflows/ci.yml`), and every repo here uses that name.

## 0. KISS & YAGNI — the first rule

The repo grows **only when a real need shows up**. One developer maintains it after hours;
every moving part is a part someone has to keep alive.

- **No dependencies "for later".** A package is added in the same change as the first code
  that uses it — Microsoft Agent Framework included: it arrives with the first evaluator
  that calls a model (README, "Roadmap"). (The base's own packages are the exception: they
  are what the gate below runs on.)
- **No configuration for scenarios that do not exist yet.** No settings, env vars, chart
  values, nginx directives or compose services until something reads them.
- **No abstraction before the second use.** Extract when the second caller arrives. No
  interface with one implementation "for testability" — the tests use the real thing (a
  real PostgreSQL) or a recorded answer.
- **No new component without a reason stated in the change.** Database, queue, cache,
  worker, sidecar — each one arrives with the feature that needs it.
- **Prefer deleting to adding.** If a change makes something unused, remove it in the same
  change.
- **When in doubt, leave it out** — and say in the change what was left out and why.

## 1. What lives where

The projects sit side by side at the root, each in a PascalCase folder named like the
project, as in Microsoft's "React and ASP.NET Core" template — no `src/` and `tests/`: the web
app keeps its tests beside its code, so those folders would only split the .NET part.

| Path | Holds |
| --- | --- |
| `AgentEval.slnx` | the .NET solution: the server and its tests |
| `global.json`, `Directory.Build.props`, `Directory.Packages.props` | the SDK band; the settings every C# project shares (nullable, analyzers, warnings as errors, lockfiles); every NuGet package and its version, named once |
| `AgentEval.Server/` | the HTTP API (ASP.NET Core minimal APIs, PostgreSQL through Npgsql, SQL written by hand and mapped by Dapper). One folder — one namespace — per part: `Api/`, `Database/`, then one per feature |
| `AgentEval.Server/Database/Migrations/up/` | numbered SQL migrations, applied at start-up by grate (`Database/Migrator.cs`) |
| `AgentEval.Server.Tests/` | the server's tests (xUnit v3), mirroring the server's folders; `ContractsTests.cs` holds the import contracts |
| `AgentEval.Web/` | the web app (React, Mantine, Vite). Source in `src/`, one folder per feature |
| `deploy/chart/` | the Helm chart the cluster runs: server, web, ingress and `templates/smoke-test.yaml`, the post-deploy smoke test |
| `compose.yaml` | the whole stack on a laptop, from the same Dockerfiles — no cluster needed |
| `AgentEval.Server/Dockerfile`, `AgentEval.Web/Dockerfile` | the two images; build context is the repo root |
| `justfile`, `.just/` | commands for humans: the main recipes and the `just <module>` families (section 10) |
| `scripts/.internal/` | everything the gate, CI and `just` run (section 10) |
| `.github/workflows/ci.yml` | CI: the gate, the browser tests, then the images |
| `docs/claude-artifacts/README.md` | links to the design documents kept as Claude artifacts, each with what it holds in a sentence or two. A new artifact about this project gets its row in the same change |
| `.artifacts/` | everything generated (e2e reports, renders); outside git and the images |
| `.editorconfig`, `.csharpierrc.json`, `.config/dotnet-tools.json`, `.gitattributes` | the shared standards: UTF-8 and LF everywhere, the C# analyzer settings, the C# formatter and its pinned version, binaries never diffed as text, lockfiles and `openapi.d.ts` marked generated. A new binary or generated file type gets its line in `.gitattributes` |

## 2. Three ways to run it — all three always work

1. **On the host, while developing:** `just db up`, `just server`, `just web` →
   http://localhost:3230. Fastest loop: `dotnet watch` and Vite rebuild on save.
2. **The container stack:** `just up` → http://localhost:8093. The same images the cluster
   runs, wired the same way (web proxies `/api` to server), with its own database volume.
   This is how a change is proven to work *without deploying it*.
3. **The cluster:** `deploy/chart/` installed by Argo CD from the platform repo, which sets
   the image tag and creates the secrets.

**The repo always carries all three:** both Dockerfiles, `compose.yaml` and `deploy/chart/`
**with its smoke test** (`templates/smoke-test.yaml`).
They are not optional extras added "when we deploy" — a project without them cannot be run
the way production runs it. Consequences:

- **A new service** (a converter, a cache, a worker) is added to `compose.yaml` **and** to the
  chart in the same change, wired the same way: same names, same env vars, no published port
  unless the browser talks to it.
- **A new setting** is configuration the way ASP.NET Core does it: committed defaults in
  `appsettings.json`, laptop values in `appsettings.Development.json`, the rest from the
  environment (`Section:Key` read as `Section__Key`: `ConnectionStrings__Default`). In code it
  is an options class of the feature that reads it (`ScenarioOptions`), bound with
  `AddOptions<T>().BindConfiguration("Section")`, validated with `.Validate(...)` or data
  annotations and **`.ValidateOnStart()`** — a missing or malformed value stops the server at
  start, never a request later. The value for compose goes in `compose.yaml`, for the cluster in
  the chart (section 7 for secrets). The database is the one exception to the section rule:
  it is a connection string, `ConnectionStrings:Default`, read into `DatabaseOptions`.
- **Images build from the repo root** with the committed lockfiles (`packages.lock.json`,
  `pnpm-lock.yaml`) in locked mode; every image pins its base and every external image is
  pinned to a version, never `latest`.
- The gate renders the chart (`helm lint` + `helm template`) and validates `compose.yaml`
  (`docker compose config`); `just up` is the manual proof that the images really start.
- **Containers go through `docker` and `docker compose`** — the scripts, `just` and CI call
  nothing else. On the owner's Fedora that is podman behind the `docker` command (podman-docker,
  rootless) with the official Compose v2 plugin, not `podman-compose` (it has gaps, e.g.
  `profiles:`); in CI it is Docker itself. solid-app-tpl calls `podman` directly; this repo
  does not.

## 3. The quality gate

`./scripts/.internal/check.sh` is **the** gate: CI runs exactly this script, `just check` is
the same thing for humans. Run it before calling any change done. It prints a PASS/FAIL/SKIP
report and exits non-zero on any failure.

| Step | Guards | In solid-app-tpl |
| --- | --- | --- |
| build server | the compiler with nullable reference types, every .NET analyzer (`AnalysisLevel` `latest-all`, the IDE style rules too) and NuGet's vulnerability audit; any warning is an error (`Directory.Build.props`). A rule that does not fit is switched off in `.editorconfig` with the reason, never with `#pragma`; a single justified exception is a `[SuppressMessage(..., Justification = ...)]` on that member | ruff, pyright strict |
| format server | `csharpier check` — C# (and the project files) formatted by CSharpier, 140 columns. `just fmt` fixes it | ruff format |
| test server | xUnit v3 against a real PostgreSQL (`just db up` locally, a service in CI), incl. `ContractsTests.cs`: the import contracts, and every server namespace has a layer | pytest, import-linter |
| tsc (app + node) | TypeScript strict plus the extra flags in `tsconfig.app.json`; `tsconfig.node.json` covers the Vite config and the e2e tests | same |
| eslint | typescript-eslint `strictTypeChecked` + `stylisticTypeChecked`, react-hooks, and the web import contracts (section 4) | same |
| prettier | web formatting: every file of the web app (TS, TSX, JSON, CSS, HTML, YAML) is checked with `prettier --check` and fails the gate when unformatted. `just fmt` (or format-on-save in VS Code) fixes it; the style lives only in `.prettierrc.json` | same |
| vitest | web unit tests (`<name>.test.ts`) | same |
| knip | dead code on the web side: unused files, exports and dependencies. An `ignore` in `knip.json` needs a reason next to it | same |
| API types | `AgentEval.Web/src/api/openapi.d.ts` matches the server's OpenAPI document (`just api-types`) | same |
| helm | the chart lints and renders, and every annotation and label name fits Kubernetes' 63-character limit after the slash (the API server would refuse it only at deployment) | same |
| shellcheck | the scripts and the git hooks | same |
| gitleaks | no secret anywhere in the git history, nor in uncommitted changes. The `.githooks/pre-commit` hook runs it on every commit too (enabled by `just sync`); CI installs a pinned version, so there it never skips | same |
| compose | `compose.yaml` resolves | same |

**Browser tests** are separate: `./scripts/.internal/e2e.sh` (`just e2e`) runs Playwright
against a real server and an empty database of its own, on ports apart from the ones you
develop on. A test that fails in CI is retried once, only so the report tells a steady failure
from a flaky one; passing on the retry still fails the job (`failOnFlakyTests`) — flaky is a
bug, in the test or in the app. **CI** runs three jobs: `test` (the gate), `e2e`, and `build`,
which builds the images only when both are green and publishes them only from `master`.

**After deploy**, Argo runs `deploy/chart/templates/smoke-test.yaml` (a PostSync Job with
curl): a handful of read-only requests through the real Services, the way traffic arrives
from the tunnel. A failure turns the sync red and the Job's log says what broke. **Every
chart has this Job** — it is the only check that runs against the real cluster, with its
real secrets, database and routing. It stays small and fast: status codes and a fragment of
the body, no logging in, no writes. Today it checks that nginx answers (`/healthz`) and that
the page reaches the server, which only starts with its database (`/api/health`).

A step that cannot run (a tool missing on a fresh machine) reports **SKIP**, never PASS.
Never weaken a step to make a change pass — fix the change.

### What the repo can prove, the gate proves

Anything that can be checked from what is committed — file names and layout, configuration
files, migration scripts, the OpenAPI document, which namespace uses which — is checked by a
test or a gate step, **not by code the server runs**. The server trusts what the gate already
proved and stays thin: no start-up code that validates its own files, no defensive branches for
a state the repo cannot be in. Runtime validation is for what only exists at runtime: input
from outside, configuration from the environment (`ValidateOnStart`), the database's state.
Example: `MigrationFilesTests` checks the migration names; `Migrator` just runs them.

### A new feature brings its checks along

Every kind of check the repo has applies to new code too. Before calling a feature done, go
through the list and add what fits — or say in the change why something does not apply:

- **Types** strict on both sides; no `object`/`dynamic`/`unknown` escape hatches and no `!`
  (null-forgiving) without a reason. A closed set of values is a type of its own — an `enum`
  on the server, serialized by name (its names are what the database and the API carry), a
  union of string literals on the web — never a bare `string` with the allowed values in a
  comment.
- **Formatting:** CSharpier on the server, Prettier on the web — run `just fmt` before the
  gate; never hand-format against them or add files to `.prettierignore` to dodge them.
- **Unit tests** for every type with rules: `AgentEval.Server.Tests/` mirrors the server's
  folders (`Database/Migrator.cs` → `Database/MigratorTests.cs`), `<name>.test.ts` sits beside the
  file on the web.
- **A browser test** (`<name>.e2e.ts` beside the code) when users click it: the path a user
  relies on, not every button.
- **Import contracts:** the new namespace or folder gets its place (section 4), in the same change.
- **Dead code:** knip stays clean, the analyzers report unused private members; remove what
  the change made unused.
- **Database:** schema changes go into a new numbered script in `Database/Migrations/up/`
  (`NNNN_what_it_does.sql`), never an edited one — grate refuses to start on a script that
  changed after it ran; the queries get tests against the real PostgreSQL.
- **API:** after changing a record the API sends or receives, regenerate `openapi.d.ts`
  (`just api-types`).
- **An outside service** (a model provider behind Microsoft Agent Framework, an API, a public
  dataset — anything not ours): the gate tests the code against a recorded answer, but the
  service can change its answer without a commit here. So the first feature that calls one
  brings, in the same change:
  - **a live test** — the real call, marked `[Trait("live", "true")]` and left out of the
    gate (the same change adds the filter to `check.sh`), run by
    `scripts/.internal/live-check.sh`;
  - **a daily check from production's network** — a CronJob in the chart, the server's image with
    another command, asking the real services through the production code and database inside a
    transaction it rolls back; when something is wrong it posts to the project's Discord alerts
    channel (a webhook in a Secret the platform repo's `setup.sh` creates). Not a GitHub workflow:
    a service can block GitHub's runners while answering production, and then the alarm is about
    GitHub. Keep it light — once a day, a small area, no retries — within each service's terms;
  - **a failure that degrades, not breaks:** reading the answer counts as part of the call — a
    missing field or a new shape is handled like the service being down (keep the last good data,
    say it may be old), and nothing stored is replaced until the new answer has been read whole.

  `grzyby-mcp` is the reference — read it in `~/repos/grzyby-mcp/`, or at
  https://github.com/Manomenu/grzyby-mcp when it is not on this machine:
  `grzyby_server/grzyby_server/miejsca/live.py` (the check), `deploy/chart/templates/live-check.yaml`,
  `grzyby_server/tests/live/`, `grzyby_server/grzyby_server/lasy/zakazy.py`. Its daily check caught
  a public dataset dropping a field the day it happened — and, run from GitHub, was then blocked
  there, which is why it moved to the cluster.
  For a model the "recorded answer" matters twice: an evaluation must be repeatable, so the
  gate never calls a model, and a judge's verdicts are tested against stored model output.
- **Deployment:** compose and the chart learn about the new service or setting (section 2).
- **Smoke test** (`deploy/chart/templates/smoke-test.yaml`): add a line when the feature
  brings something that can break only on the cluster and can be checked without logging
  in — a new service or dependency, a new secret or config read at start-up, a new public
  route, a change to nginx routing. Anything behind a login belongs in the e2e tests.

## 4. Import contracts: which part may import which

Both sides are layered: a part may use the parts **below** it, never above it or beside
it. "Below" means more basic — knows less about the rest. The order is checked by the gate,
not by reviewers; a dependency against it fails with the types involved.

| | Server | Web |
| --- | --- | --- |
| Where | `Layers` in `AgentEval.Server.Tests/ContractsTests.cs` (ArchUnitNET, on the compiled assembly) | `boundaries/*` in `AgentEval.Web/eslint.config.js` |
| Today | `Api` → `Database` | `main`/`App` → features → `api` |
| A new feature | **must** get a layer — `Every_server_namespace_has_a_layer` fails otherwise | is a folder `src/<feature>/`, independent of other features by default |
| Inside a feature | a feature with several concerns declares its own order (e.g. endpoints → store → model) as a test beside the first | free |
| Feature uses feature | only downwards, by its position in the layer list | only through an explicit policy in `eslint.config.js`, with a comment saying why |

`ContractsTests.cs` and `eslint.config.js` each carry a commented example of adding a feature.
The check reads compiled code: a `const` or a `nameof` leaves no trace there, a type used in
a signature, a field or a call does.

- **Respect the order.** If code seems to need a dependency against it, the code is in the
  wrong place: move it down to a shared level, or pass it in from above — do not loosen a
  contract to make it pass.
- **A contract change is a design change.** Say in the change why the new edge exists.

## 5. Layout: by feature, the same on both sides

**The app grows by features.** This is the preferred — default — way to extend it: the
code that makes one feature work is kept together in **one folder**, on each side, named
after the feature (`Scenarios/`, `Evaluations/`, `Judges/`), not spread across folders by
technical kind (`Models/`, `Services/`, `Controllers/`, `components/`, `hooks/`). A folder
holds everything that changes together: on the server its endpoints, records, SQL and tests
mirror; on the web its screen, components, hooks, logic, HTTP calls and their tests beside them.

Why: a change to one feature then touches one folder per side, a feature can be read,
reviewed, moved or deleted as a whole, and the import contracts (section 4) can keep
features apart. A layer-first layout does the opposite — every change cuts across all of it.

In practice:

- **A new capability starts as a new folder**, or goes into the existing feature it
  belongs to — never into a shared bucket. Name it in the users' terms, the same on both
  sides, so `AgentEval.Server/Scenarios/` and `AgentEval.Web/src/scenarios/` are obviously
  one thing (PascalCase for the C# namespace, camelCase for the web folder).
- **Shared code is the exception and is earned:** it moves down to a shared level only when
  a second feature really needs it, and only as plumbing (`Database/`, `api/client.ts`) — never as
  a grab-bag of helpers.

A change to one feature should touch one place on each side.

- **Server:** a feature is a folder and a namespace. With more than one concern it splits by
  role — `ScenarioEndpoints.cs` (the routes, an `app.MapScenarios()` extension that
  `Api/ServerApp.cs` calls), `ScenarioStore.cs` (the SQL), `Scenario.cs` (the records the API
  sends and receives); a single concern stays one file. Types are `internal` (the analyzers
  insist); the tests see them through `InternalsVisibleTo`.
- **One operation, two front doors: HTTP and MCP.** The API is used by the web and by agents
  (Claude Code through the MCP endpoint, README "Two front doors"). An operation's logic lives
  once, in the slice (`ScenarioOperations.cs`, or the store when it is only a query), and
  returns a record or a domain error. Two thin registrations call it: `ScenarioEndpoints.cs`
  maps the result to HTTP (status codes, Problem Details), `ScenarioTools.cs` to an MCP tool
  (`[McpServerTool]`, a description written for a model, errors as tool errors). No layer that
  turns every endpoint into a tool by itself: not every endpoint is a good tool, and a tool's
  description is not an endpoint's. A helper that registers both comes when the second slice
  shows the same lines twice.
- **Queries: SQL by hand, Dapper maps it.** A store writes plain SQL and lets Dapper map the
  rows onto its row records (`connection.QueryAsync<ScenarioRow>(sql, new { suiteId })`).
  Parameters always as `@name`, never concatenated into the text. No ORM (EF Core, linq2db)
  and no SQL generator: the SQL a reader sees is the SQL PostgreSQL runs, and the tests
  against the real database check it.
- **Server data types — the contract and the inside.** A record an endpoint sends or receives
  is a contract with someone outside: it becomes the OpenAPI schema and the web app's types, so
  renaming a property breaks a client, and input from outside is validated where it enters. A
  record that lives only inside the server — a row from our own SQL, an intermediate result —
  changes freely. Keep the two apart (a store returns its own row type, the endpoint maps it),
  so a reader can tell whether changing a type changes the API.
- **Web:** one folder per feature under `src/`, holding its components, hooks, logic and its
  HTTP calls (`<feature>/api.ts`, typed from `api/openapi.d.ts` — no hand-written copies of
  server models). `api/` is the only shared folder: HTTP plumbing.
- **No catch-all folders** (`Common/`, `Helpers/`, `Utils/`, `Shared/`, `ui/`, `components/`).
  A piece that seems to belong nowhere usually belongs with others that change together: name
  that feature and give it a folder.
- **Logic out of components.** Anything with rules is a plain `.ts` module with a
  `.test.ts` beside it; components and hooks only wire it to React.
- **Files:** C# — one top-level type per file, named after it, in the namespace its folder
  path spells (`AgentEval.Server/Database/Pool.cs` → `AgentEval.Server.Database`); the build
  fails otherwise (IDE0130, `.editorconfig`). Web — a component per
  `PascalCase.tsx`, hooks `useSomething.ts`, other modules `camelCase.ts`. Shared e2e steps,
  once there are some, live in `AgentEval.Web/e2e/helpers.ts`.
- **State:** component state and props. No global store and no data-fetching library until
  a second screen needs shared server state.

### 5.1 The web app (React) — practices

The frontend is React 19 with Mantine and Vite, TypeScript strict. `src/health/` is
the base's only feature and shows the pattern end to end: `api.ts` → `status.ts` (+ test) →
`ServerStatus.tsx` (+ e2e test).

- **One feature, one folder**, holding its screen, hooks, logic and `api.ts`. `App.tsx` only
  puts features together; `main.tsx` only sets up providers and the theme.
- **Types come from the server.** A feature's `api.ts` calls `request<T>()` from
  `api/client.ts` with `T` taken from `api/openapi.d.ts`. No hand-written copies of server
  models, no `fetch` outside `api/client.ts`, no host names: the browser always calls `/api`
  on its own origin (the Vite proxy in development, nginx in the image).
- **Logic out of components.** Rules (parsing, formatting, state machines, saving) are plain
  `.ts` modules with a `.test.ts` beside them; components and hooks only connect them to
  React. If a component needs a test of its own to cover a rule, the rule is in the wrong
  file.
- **Effects are safe to run twice.** `StrictMode` runs every effect twice in development:
  every request in an effect gets an `AbortController` and ignores its result once aborted;
  one-off actions (a notification, a migration of stored data) happen where code runs once,
  not in a component that may mount twice.
- **Never block the user on the network.** Typing and clicking change local state at once;
  saving happens in the background — debounced, one request in flight, retried when it
  fails — and a small status shows where it stands (saving / saved / offline), instead of a
  spinner over the form. A conflict (409) is shown to the user, never silently overwritten.
- **Errors reach the user in their language.** `api/client.ts` turns every failed response
  into an `Error` with the server's `detail`; a validation error (422) maps onto the fields
  it belongs to. No raw stack traces, no silent `catch`.
- **Mantine first.** Layout and spacing through Mantine components and props, colours from
  the theme (`main.tsx`). A global stylesheet (`src/app.css`) only when Mantine has no prop
  for it, with classes named after the feature (`.draft-row`), not inline style objects that
  are repeated.
- **Accessible names are the test contract.** Interactive things are real buttons, links and
  inputs with a visible label or an `aria-label` (an icon button: `aria-label="Konto"`).
  That is what screen readers read and what the e2e tests find them by.
- **Numbers that shape the layout are named constants** (`NAVBAR_WIDTH`), exported, and tests
  compute expectations from them — a test that hard-codes 220 breaks on a design tweak.
- **The browser's storage is for conveniences only** (a panel width, the last tab): read it
  defensively (it can be empty, full or blocked) and validate what comes back. Anything the
  user would miss lives on the server.
- **Browser tests** (`<name>.e2e.ts` beside the code) find elements by role and visible text,
  use `exact: true` (a substring match passes on the wrong text), create their own data and
  never depend on another test's. They test what a user relies on, not every button.
- **Dependencies:** Mantine's packages come in only when used (`@mantine/dates` with the
  first date field, `@mantine/notifications` with the first notification), each in the same
  change as its provider and stylesheet in `main.tsx`.

## 6. File nesting: a file and its companions are one entry

Tests and companions sit **beside** the file they belong to and share its name, so the
editor folds them under it (`.vscode/settings.json`, `explorer.fileNesting.patterns`):

```
drafts.ts               ▸ drafts.ts
drafts.test.ts            ├ drafts.test.ts
drafts.e2e.ts             └ drafts.e2e.ts
Panel.tsx               ▸ Panel.tsx
Panel.test.ts             ├ Panel.test.ts
Panel.e2e.ts              └ Panel.e2e.ts
```

- Name companions `<name>.<role>.ts` — `.test.ts` (vitest), `.e2e.ts` (Playwright),
  `.stories.tsx` etc. — never `test_<name>.ts` or a separate `__tests__/` folder; a
  differently named file falls out of the nesting.
- Configuration nests under its owner: `AgentEval.slnx` ▸ `global.json`, `Directory.*.props`,
  `.csharpierrc.json`; a `.csproj` ▸ its `packages.lock.json`; `package.json` ▸ lockfile,
  eslint, prettier, knip, tsconfig, vite; `appsettings.json` ▸ `appsettings.*.json`; `README.md` ▸
  the repo-level files.
- A new kind of companion or config file gets a pattern in `.vscode/settings.json` in the
  same change, rather than cluttering the explorer.
- C# tests cannot sit beside the type (they would compile into the server and its image), so
  `AgentEval.Server.Tests/` mirrors the server's folders instead — same names,
  `<Type>Tests.cs`.

## 7. Secrets

**No secret value ever enters this repo** — not in code, not in `appsettings.json`, not in
`values.yaml`, not in a test. `.gitignore` keeps `.env`, `.secrets/` and key files out;
gitleaks checks every commit (the pre-commit hook) and the whole history (the gate). A hit is
never silenced with an allowlist entry for a real value — remove the value and rotate it.
A model provider's API key is a secret like any other.

- **On a laptop:** `dotnet user-secrets` (outside the repo, loaded on top of appsettings in
  Development). The repo has no secret yet; how the first one arrives — user secrets, the
  compose mount, `secrets.sh` backing that file up to Bitwarden — is planned in `TODO.md`
  and built in the change that brings it. A local, non-secret value (the development
  database's password) may sit in `appsettings.Development.json`.
- **On the cluster:** secrets are owned by the platform repo (`suwalski-platform`), never
  by this one. Each lives in that repo's `.secrets/<project>[-<env>].env`, is entered once
  by `scripts/projects/<project>/[<env>/]setup.sh`, which creates the Kubernetes Secret, and
  is backed up to Bitwarden with **`just secrets backup`**; a new machine gets them back with
  **`just secrets restore`**. The chart only names the Secret (`server.databaseSecret` and
  the like) and reads it through `envFrom`.
- **A new secret, step by step:** an options class with no default for it, validated on start;
  the laptop value in user secrets;
  the Secret's name as a chart value; the prompt in the platform repo's `setup.sh` (its
  AGENTS.md, „Nowa zmienna”). **End the change by telling the owner** to run that `setup.sh`
  and then `just secrets backup` there. Agents never log in to Bitwarden or run the backup
  themselves.
- Not a secret — a URL, a port, a public name, a model's name — goes in plain sight:
  `values.yaml`, a ConfigMap, `appsettings.json`.

## 8. Network access and login: through Cloudflare

The app does **not** build its own login, sessions or passwords, and its ports are never
opened to the internet. Reaching it and logging in both go through Cloudflare, configured in
the platform repo:

- **Network access:** a Cloudflare Tunnel from the cluster — no port forwarding, no public IP.
  The chart's ingress host is what the tunnel points at.
- **Who may log in:** Cloudflare Access in front of the address — a one-time code by email
  (or a Google/GitHub login) for an allow-list of addresses kept in the platform repo
  (`terraform/edge/access/<group>.json`, filled by the project's `setup.sh`). Adding a
  user is a change there, not here.
- **Who is logged in, inside the app:** every request carries Access's signed token in the
  `Cf-Access-Jwt-Assertion` header. The server **verifies** it (signature from the team's
  public keys, the application's audience tag) and takes the email from it — never trusts a
  plain email header. The team domain and audience come in as config (a ConfigMap from the
  platform repo), not secrets. `automat-operat`'s `identity.py` (Python) is the reference
  implementation — here the same checks become an ASP.NET Core authentication handler: the
  current user for the endpoints, `/me` for the web app, and logout as
  a link to `/cdn-cgi/access/logout`.
- **On a laptop** there is no Cloudflare: a `DEV_USER_EMAIL` setting stands in for the token,
  only when set, and never on the cluster.
- **Public parts** (a landing page, a public API or MCP endpoint) get an Access *bypass*
  policy for their path in the platform repo, and their own protection where it matters:
  rate limiting in Cloudflare, read-only endpoints, no personal data.
- Until the app has users, none of this is wired: the app has no identity module yet. It
  arrives with the first feature that needs to know who is asking (section 0).

## 9. Naming

Two vocabularies, chosen word by word:

- **Domain words in the users' language.** What the users' work is made of — its things, their
  attributes, the terms of the trade — is named the way the users say it. The trade here is
  evaluating LLM agents, and its terms are English even in Polish conversation: `Scenario`,
  `Conversation`, `ToolCall`, `RetrievedChunk`, `Assertion`, `Judge`, `Verdict`, `Run`. Pick
  one word per thing and keep it everywhere — server, web, database, README.
- **Software words in technical English.** Everything that exists only because there is
  software: files named by their role (`ScenarioStore.cs`, `ScenarioEndpoints.cs`, `api.ts`),
  verbs (`Fetch`, `Replace`, `Search`, `Export`), generic fields (`Id`, `Name`, `Kind`,
  `CreatedAt`), infrastructure (caches, jobs, settings, env vars, chart values), API routes,
  test helpers, CSS classes.
- **C# conventions on the server, the web's on the web:** PascalCase types, members,
  namespaces and the solution's folders (`AgentEval.Server/Database/`), camelCase in JSON (ASP.NET Core's default), snake_case tables and columns in
  PostgreSQL (`scenarios`, `created_at`).
- **Abbreviations: only well-established ones, and only as part of a longer name.** `Id`,
  `Http`, `Json`, `Sql`, `Url`, `Db` inside `DbConnection` or `DbContext` are fine; a name that
  is nothing but an abbreviation is spelled out — the folder is `Database/`, not `Db/`, a
  setting is `ConnectionString`, not `ConnStr`. A word the team would have to decode is never
  shortened.
- **Text people read** is in the users' language regardless: labels and messages on the web
  (Polish, like the rest of the owner's apps).

## 10. Commands: `justfile` vs `scripts/.internal/`

- **`justfile` is for humans.** `just` lists every recipe, grouped: `run` (dev servers),
  `dev` (code generation), `infra` (the container stack), `maintenance` (check, e2e, fmt,
  sync), plus modules in `.just/`: `db` (`.just/db.just`: the local PostgreSQL) and
  `secrets` (`.just/secrets.just`: the `.env` files in Bitwarden). New human-facing commands
  go there, into the matching group; a family of related commands becomes a module of its
  own — a file in `.just/`, mounted with `mod <name> '.just/<name>.just'` and starting with
  `set working-directory := ".."`, so its recipes run from the repo root like the rest.
- **`scripts/.internal/` is for machines** — CI, `just` recipes and agents. Logic longer than
  a line or two lives here as a `.sh`, and the recipe only calls it.
- **Agents call these directly:**

  | Script | Use it to |
  | --- | --- |
  | `scripts/.internal/check.sh` | verify a change — the whole gate (section 3) |
  | `scripts/.internal/e2e.sh [playwright args]` | browser tests (`AgentEval.Web/src/**/*.e2e.ts`) against a real server and an empty `agenteval_e2e` database, on ports 6231/3231. Run it after changing anything a user clicks |
  | `scripts/.internal/db.sh up\|down\|status\|psql` | the local PostgreSQL on `localhost:5463` (docker container `agenteval-postgres`). `check.sh` runs `up` itself; tests create their own `agenteval_test` database |
  | `scripts/.internal/api-types.sh [--check]` | regenerate `AgentEval.Web/src/api/openapi.d.ts` after changing a record the API exposes. Never edit that file by hand |
  | `scripts/.internal/secrets.sh backup\|restore` | **not for agents** — the owner's copy of the `.env` files in Bitwarden (`just secrets`, section 7); it asks for the master password |
  | `scripts/.internal/infra-status.sh` | what of the compose stack is up and on which ports (needs `jq`) |

  A new helper an agent should reach for goes here too, with a row in this table.
- **Plain `dotnet` works too:** `dotnet build AgentEval.slnx`, `dotnet test --solution
  AgentEval.slnx` (Microsoft.Testing.Platform, set in `global.json`) — the gate is these plus
  the rest of section 3.

## 11. Behavioral guidelines

Guidelines to reduce common LLM coding mistakes.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

### 11.1 Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

### 11.2 Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

### 11.3 Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan with a check per step. Simplicity rules are in
section 0.

### 11.4 Delegate well-defined work to a cheaper model

**Larger, well-defined tasks go to a subagent on Sonnet** — to save tokens. Well defined
means: a clear goal, the files it touches are known, and a check says when it is done
("port X to Y with `check.sh` green", "write the tests for Z", "rename A to B everywhere").

- The prompt carries everything the subagent needs: the goal, the files, the rules of this
  file that apply, and the acceptance check. It starts with no context of its own.
- The main agent keeps what needs judgment: design decisions, unclear requirements, small
  edits (spawning costs more than doing them), and the review of the subagent's result
  against the acceptance check — without redoing the delegated work.
