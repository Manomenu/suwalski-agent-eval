# Development

Working on Suwageval itself. To only use Suwageval, the [README](../README.md) is enough.

## Requirements

.NET SDK 10, `pnpm` (via corepack), `just`, `docker` with `docker compose` (on Fedora:
podman behind the `docker` command, with the Compose v2 plugin); for the full gate also
`helm`, `shellcheck`, `gitleaks` and `oasdiff`. After cloning: `just sync` (dependencies, the gate
`suwgate` with the web app's packages, the C# formatter and the git hooks).

On Fedora (the owner's development machine):

```sh
sudo dnf install dotnet-sdk-10.0 nodejs nodejs-corepack podman podman-docker just helm ShellCheck
corepack enable --install-directory ~/.local/bin   # pnpm, in the version package.json pins
```

`docker compose` is the official Compose v2 plugin (not podman-compose), talking to podman's socket. gitleaks and oasdiff are the
binaries of their GitHub releases, in the versions suwgate's CI workflow pins.

## Everyday commands

```sh
just                 # every recipe, grouped
cp .env.example .env # once: the model Suwageval calls (Models__fallback__*: address and name), never committed
just db up           # local PostgreSQL on :5463 (compose profile db; just db status|psql|down|reset)
just server          # API on :6230 (OpenAPI document at /openapi/v1.json)
just web             # web app on :3230, proxies /api to the server
just up              # the whole stack in containers on :8093 — no cluster needed
just build-images    # only build suwageval-server:local and suwageval-web:local, for another repository's compose stack
just check           # the quality gate (suwgate check, the browser tests too), exactly what CI runs
just e2e             # only the browser tests (suwgate step e2e), with what they need; --headed, -g <name> go to Playwright
just check-quick     # the gate while working: the guards, and build, csharpier, the guard tests, eslint, tsc, vitest for what changed; no database
just cli validate DIR  # the suwageval CLI from source; `just cli-install` installs it globally
just tmux-dev start  # tmux layout: server, web, the example agent suwexag (from source with `dotnet watch`, else its `suwexag:local` image), spare shell, CLI shell (SUWAGEVAL_AGENT_DIR sets the agent folder)
just tmux-dev start --restart  # the same, after killing the old session and everything running in it
just tmux-dev stop   # kill the session and everything running in it
just live-check      # only the live tests, against the real model of .env
just secrets backup  # copy the local .env files into Bitwarden (restore on a new machine)
just agent-publish VERSION  # release Suwageval.AgentSdk to nuget.org through its workflow, no key, permanently: release.md
just agent-publish-local VERSION  # the fallback, with a key from .env: a first release the workflow cannot make, or another feed
```

The quick gate has a budget (suwgate's, 20 s) and fails above it, so what is slow runs only in the full gate: the server's tests. `SUWGATE_QUICK_LIMIT=<seconds>` raises the budget on a machine that is slow across the board. The scripts behind these and `suwgate` itself answer `--help`. Plain `dotnet` works too: `dotnet build Suwageval.slnx`, `dotnet test --solution Suwageval.slnx`.

New human-facing commands go into the matching group of the `justfile`; a family of related commands becomes a module beside it (`<name>.just`, `mod <name>` in the `justfile`).

## Worktrees

A linked git worktree gets its own test and e2e databases and its own e2e ports, derived from its directory name by `suwgate dev checkout-env`, which `suwgate check` applies to its .NET and browser tests; its databases are left behind, not dropped. A value already in the environment wins. The server's tests refuse to run in a worktree without its own `TEST_CONNECTION_STRING`, so a plain `dotnet test` there fails with the fix instead of using the main checkout's database.

## Claude Code hooks

The committed Claude Code settings run the quick gate when Claude stops (`suwgate hook stop`) and block the stop while it fails, once per turn. The quick gate costs what the change since master costs: the .NET steps run only when a .NET file changed (and test only the projects of the guards), the web steps only for changed web code. A turn that left HEAD, the changes and the untracked files as they were at the last green run does not run it at all. `suwgate hook guard-edit` refuses edits to the gate (`suwgate.yml`, `layers.json`, the tool configs, `Suwageval.Gate/`), existing migrations, lockfiles, `openapi.d.ts` and `contract/openapi.json` with a message saying what to do; `ALLOW_GATE_EDIT=1 claude` lifts the gate's part for one session.

## Guardrails of acceptance criteria

Where each kind of guardrail lives and how it names the id (the kinds come with the profiles of `suwgate.yml`; `criteria.json` adds none, it only names the files read as prose). Pick the lightest kind that can fail for the criterion.

| Kind | Where | How it names the id |
| --- | --- | --- |
| `test (xunit)` | `Suwageval.Server.Tests/`, `Suwageval.Cli.Tests/`, `Suwageval.Gate/` | `[Trait("criterion", "AC-<feature>-<n>")]` on the test |
| `web test (vitest)` | `Suwageval.Web/src/**/*.test.ts(x)` | in the title: `it("AC-<feature>-<n>: …", …)` |
| `e2e test (playwright)` | `Suwageval.Web/src/**/*.e2e.ts` | in the title: `test("AC-<feature>-<n>: …", …)` |
| `lint (eslint)` | `Suwageval.Web/eslint.config.js` | a comment `// AC-<feature>-<n>` on the line above the rule |
| `type check (tsc)` | `Suwageval.Web/tsconfig*.json` | a comment `// AC-<feature>-<n>` above the option |
| `lint (.NET analyzer)` | `.editorconfig` | `# AC-<feature>-<n>` on its own line above `dotnet_diagnostic.….severity = error` |
| `build rule` | `Directory.Build.props` | an XML comment `<!-- AC-<feature>-<n> -->` above the property |

## A new feature brings its checks along

Every kind of check the repo has applies to new code too. Before calling a feature done, go
through the list and add what fits — or say in the change why something does not apply:

- **Types** strict on both sides; no `object`/`dynamic`/`unknown` escape hatches and no `!`
  (null-forgiving) without a reason. A closed set of values is a type of its own — an `enum`
  on the server, serialized by name (its names are what the database and the API carry), a
  union of string literals on the web — never a bare `string` with the allowed values in a
  comment.
- **Formatting:** CSharpier on the server, Prettier on the web — run `just fmt` before the
  gate; never hand-format against them or add files to `.prettierignore` to dodge them.
- **Unit tests** for every type with rules: `Suwageval.Server.Tests/` mirrors the server's
  folders (`Database/Migrator.cs` → `Database/MigratorTests.cs`), `<name>.test.ts` sits beside the
  file on the web.
- **A browser test** (`<name>.e2e.ts` beside the code) when users click it: the path a user
  relies on, not every button.
- **Import contracts:** the new namespace or folder gets its place, in the same change.
- **Dead code:** knip stays clean, the analyzers report unused private members; remove what
  the change made unused.
- **Database:** schema changes go into a new numbered script in `Database/Migrations/up/`
  (`NNNN_what_it_does`), never an edited one — grate refuses to start on a script that
  changed after it ran; the queries get tests against the real PostgreSQL.
- **API:** after changing a record the API sends or receives, regenerate `contract/openapi.json`
  and `openapi.d.ts` (`just api-types`) and raise the version in `contract/package.json`: minor
  for an addition, major for a break. Installed CLIs lag behind the server, so the gate's
  `api-version` step (oasdiff) fails a break the version does not say.
- **An outside service** (a model provider behind Microsoft Agent Framework, an API, a public
  dataset — anything not ours): the gate tests the code against a recorded answer, but the
  service can change its answer without a commit here. So the first feature that calls one
  brings, in the same change:
  - **a live test** — the real call, marked `[Trait("live", "true")]` and skipped unless the
    model is configured in the environment (the gate does not read `.env`), run by `just live-check`;
  - **a daily check from production's network** — a CronJob in the chart, the server's image with
    another command, asking the real services through the production code and database inside a
    transaction it rolls back; when something is wrong it posts to the project's Discord alerts
    channel (a webhook in a Secret the platform repo's `setup.sh` creates). Not a GitHub workflow:
    a service can block GitHub's runners while answering production, and then the alarm is about
    GitHub. Keep it light — once a day, a small area, no retries — within each service's terms;
  - **a failure that degrades, not breaks:** reading the answer counts as part of the call — a
    missing field or a new shape is handled like the service being down (keep the last good data,
    say it may be old), and nothing stored is replaced until the new answer has been read whole.

  For a model the "recorded answer" matters twice: an evaluation must be repeatable, so the
  gate never calls a model, and a judge's verdicts are tested against stored model output.
- **Deployment:** compose and the chart learn about the new service or setting ([deployment.md](deployment.md)).
- **Smoke test** (`deploy/chart/templates/smoke-test.yaml`): add a line when the feature
  brings something that can break only on the cluster and can be checked without logging
  in — a new service or dependency, a new secret or config read at start-up, a new public
  route, a change to nginx routing. Anything behind a login belongs in the e2e tests.
