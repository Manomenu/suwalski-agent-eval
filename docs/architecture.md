# Architecture

## Stack

- **Server:** .NET 10, ASP.NET Core minimal APIs, PostgreSQL through Npgsql and Dapper (SQL written by hand, numbered migrations), an OpenAPI document the web app is typed from; Microsoft Agent Framework for the judge and the user simulator.
- **Web:** React 19, Mantine, Vite, TypeScript strict.
- **Around them:** PostgreSQL 17, a container per part, compose for the whole stack on a laptop, a Helm chart with a post-deploy smoke test for the cluster.

The server is the one HTTP API: the web app and the CLI both use it, and Claude Code through the CLI; there is no MCP endpoint. The evaluation's routes live under `/api/eval/…`, the monitoring's under `/api/monitoring/…`, and what every area shares (agents, versions, `/api/health`) has no prefix. The agent under test is reached over HTTP through its two routes ([scenario-format.md](scenario-format.md)) and is never part of this repository's deployment.

## The map

One line per top-level path (at most 300 characters each). `RepositoryLayoutTests` reads these lines: a new top-level folder needs its line here, a deleted one loses it.

- `Suwageval.slnx` — the .NET solution. Projects sit side by side at the root, each folder named like its project, with no separate source and test roots: the web app keeps tests beside its code.
- `Suwageval.Server/` — the HTTP API, cut into feature folders: `Api/` wires the routes, areas hold the product's features, and shared features stand below them. Migrations are numbered SQL, applied at start-up by grate, never edited.
- `Suwageval.Server.Tests/` — xUnit v3 tests against a real PostgreSQL, mirroring the server's folders. `EndToEnd/` is the one place where the CLI runs against the server in process. The SDK's tests live here too: it has no test project.
- `Suwageval.AgentSdk/` — the NuGet package of a .NET agent under test (`net9.0;net10.0`): the agent contract, the log items, the two endpoints and the monitoring. Public API with XML documentation.
- `Suwageval.Contracts/` — the records the API sends and receives and the CLI reads from YAML, with the rules that decide validity, so the CLI and the server cannot drift. Its XML documentation becomes the OpenAPI descriptions.
- `Suwageval.Cli/` — the `suwageval` command, a .NET tool (System.CommandLine, YamlDotNet). Code is cut into areas like the server's; command names stay flat; `suwageval --help` lists them by area.
- `Suwageval.Cli.Tests/` — the CLI's tests (xUnit v3), mirroring its folders; pure unit tests, no server and no database. `Sample/` is the one agent folder the tests use.
- `Suwageval.Gate/` — the quality machinery: the gate script, design, code and repository guards, the layers, the criteria check, the Claude Code hooks. See "The gate" below.
- `Suwageval.Web/` — the web app (React, Mantine, Vite). `src/evaluation/` and `src/monitoring/` are areas with a folder per feature; the shared folders stand beside them. Practices: [web.md](web.md).
- `deploy/` — the Helm chart the cluster runs: server, web, ingress, the post-deploy smoke test and the daily live check.
- `compose.yaml` — the whole stack on a laptop from the same Dockerfiles, no cluster needed.
- `Suwageval.Server/Dockerfile`, `Suwageval.Web/Dockerfile` — the two images; the build context is the repository root.
- `claude-plugin/`, `.claude-plugin/` — the Claude Code plugin: skills, a session hook and the marketplace file. Text and one shell script; the file format is linked from the skill, not copied.
- `docs/` — documentation: this map, `development.md`, `deployment.md`, `release.md`, `web.md`, the file format, the concepts and the front doors; specs in `visual/spec/`.
- `.claude/` — the committed Claude Code settings: the Stop hook and the edit guard of the gate.
- `scripts/` — `scripts/.internal/` holds everyday tools the gate, CI and `just` run; each answers `--help`.
- `.just/` — modules of the `justfile`.
- `.github/` — `workflows/ci.yml` (gate, browser tests, then images, published only from `master`) and `workflows/agent-sdk.yml` (the SDK release; a nuget.org policy names this file, so it is never renamed alone).
- `.githooks/` — `pre-commit` runs gitleaks on what is being committed; `just sync` enables it.
- `.vscode/` — the editor's glue: recommended extensions, file nesting, launch configurations, no style rules.
- `.config/` — `dotnet-tools.json`: the pinned CSharpier.
- `.artifacts/` — everything generated (e2e reports, renders), outside git and the images.

Files at the root: `Directory.Build.props` (strict compiler and analyzers), `Directory.Build.rsp` (no idle MSBuild workers), `Directory.Packages.props` (every package version, once), `BannedSymbols.txt` (APIs the build refuses, each with what to use instead), `.editorconfig` (every disabled rule has its reason), `.csharpierrc.json`, `.gitattributes` (a new binary or generated file type gets its line), `global.json`, `nuget.config`.

## The gate

Quality machinery lives apart from what it checks, for two reasons: a guard tests the whole repository, not one project, and an agent that cannot pass a guard must not be able to change it.

- Belongs in the gate: anything that decides whether a change passes. A tool for everyday work stays in `scripts/`, even when the gate calls it.
- A hook refuses an agent's edit of the whole folder, so a new guard is protected from its first line. The owner lifts it for one session with `ALLOW_GATE_EDIT=1 claude`.
- Data that changes with every feature is the exception: `layers.json` stays editable, because its diff is one readable line and a protected file would stop every feature at the owner's door.
- Guards are grouped by what they check: design, code, the repository. Scripts sit apart from tests.
- The SDK's layers stay beside the SDK's tests: the release workflow picks those tests by class name, and its proof should be what it ran.

## What the gate forbids

Each line is a build rule or a test; its message says the fix. Rules live as low as they can: types and analyzers first.

- Build: analyzers `latest-all`, warnings are errors, IDE style rules, namespace equals folder (IDE0130), NuGet audit. A rule that does not fit is switched off in `.editorconfig` with the reason; a single exception is a `[SuppressMessage]` with a justification.
- Format: CSharpier (140 columns) and Prettier; `just fmt` fixes both.
- Tests: xUnit v3 against a real PostgreSQL, vitest beside the code; tests marked `live` call a real model and stay out (`scripts/.internal/live-check.sh`).
- Web: tsc strict, eslint (strictTypeChecked, react-hooks, import boundaries), knip (dead code), `openapi.d.ts` equal to the server's OpenAPI document.
- Containers: `helm lint` and `helm template` with the 63-character limit on annotation and label names; `docker compose config`.
- Secrets: gitleaks on the whole history, on uncommitted changes and on untracked files.
- Layers (`ServerContractsTests`, `CliContractsTests`, `AgentSdkContractsTests`): a layer uses only the layers below, every namespace has a layer, a sub-feature is used by neither its feature nor its siblings, one `*Endpoints` class per namespace.
- Project graph (`ProjectReferencesTests`): which project may reference which and see whose internals.
- Names (`NamingTests`, `ForbiddenNamesTests`): unique type names, one top-level type per file named like the file; no folder, namespace or type named Common, Helpers, Utils, Manager, Shared, Core, Domain, Services, Models, Components, Hooks, Ui and the like.
- Packages (`PackagesTests`): no MediatR, AutoMapper, Mapster, Scrutor, mocking libraries, FluentAssertions, Newtonsoft.Json or Entity Framework; every versioned package is referenced.
- Interfaces (`InterfacesTests`): at least two implementations, apart from those an agent implements.
- Deleting (`SubtractTests`): nothing `[Obsolete]`, no unused non-public server type.
- Banned APIs (`BannedSymbols.txt`, the build refuses them with what to use instead): `DateTime.Now`/`UtcNow`, blocking on tasks, `Thread.Sleep`, `Console`, `Activator`, reflection discovery, `Type.GetType(string)`, Newtonsoft.Json.
- Acceptance criteria (`criteria.sh`): every criterion of a spec has an id and a guardrail that names it, and nothing else names an id.
- Escape hatches (`EscapeHatchTests`, `NoPragmaTests`): no `dynamic`, no `#pragma warning disable`; a `!` outside tests needs a `// Why:`.
- Comments (`CommentsTests`, `web-comments.mjs`): only one-line Why comments and tool markers in C#, TypeScript and shell; XML documentation only in the SDK and the contracts.
- Settings (`AppSettingsTests`): every section of appsettings is read by an options class.
- Images (`ImagePinsTests`): every `FROM` and `image:` carries a version, never `latest`.
- Plugin and image (`ClaudePluginTests`, `ServerImageTests`): manifests, skills and the session hook are sound; the server's image copies every project it builds from.
- Scripts (`ScriptsTests`, `RepositoryLayoutTests`): each answers `--help`, each is executable, no two paths differ only in letter case.
- Docs (`AgentsFileTests`, `DocsPathsTests`, `RepositoryLayoutTests`): AGENTS.md at most 100 lines, no tree drawings, no capitals shouting, CLAUDE.md is `@AGENTS.md`, a path the docs name exists, every top-level folder is in the map.
- Editor (`VsCodeSettingsTests`): no style keys in the settings; recommended extension ids are well formed.
- Hooks (`HookGuardEditTests`): the gate, migrations and generated files are refused with the fix.
- The gate itself (`check.sh`): a ceiling per step; a SKIP fails in CI and is `NOT VERIFIED` locally; the quick gate fails above its time budget (`CHECK_QUICK_LIMIT`).

## Import contracts: which part may use which

A part may use the parts below it, never above it or beside it. "Below" means more basic. The order is data in `Suwageval.Gate/layers.json` for the server and the CLI, and `boundaries/*` in `Suwageval.Web/eslint.config.js` for the web; a dependency against it fails with the types involved. The check reads compiled code: a `const` or a `nameof` leaves no trace there, a type used in a signature, a field or a call does.

- **Areas.** An area (`Evaluation`, `Monitoring`) groups the features of one product area: one entry in the top order, with an order of its own inside. An area never uses another area. Features every area needs stand below the areas and never use one; what a second area needs from the first moves down into a shared feature. `Api` and the parts above the areas compose them.
- **The CLI** is layered the same way. The commands of an area are siblings, none uses another; what two share moves down. Command names stay flat on purpose (`suwageval run`): evaluation is the daily work, so only the code follows the areas, and `HelpSections` shows them as sections.
- **The web.** A feature of an area imports itself, the shared folders and the features of its own area named in an explicit policy; a shared folder never imports an area. A new web feature is a folder the rule already covers.
- **Projects** point one way (`projects` in `layers.json`): the CLI and the server know the contracts and nothing of each other; only the server's tests know the CLI, for the end-to-end tests.
- **A contract change is a design change.** Say in the change why the new edge exists.

## How code is split

The app grows by features: the code that makes one feature work sits in one folder on each side, named in the users' terms (scenarios, compare, trend), not by technical kind. A change to one feature then touches one place per side, and a feature can be read, moved or deleted as a whole. The word is the same on every side; the tree need not be: the web cuts by screens, the CLI by commands, the server by routes and data.

- A new capability starts as a new folder or goes into the feature it belongs to, never into a shared bucket. Shared code is earned by the second feature that needs it and is named after what it is (`Output/`), not after who uses it.
- When a feature grows, the signs are content, not file counts: a second set of routes, a group of files that change together with a shared name prefix, a part the web or the CLI already has as a folder of its own.
- A part built on its feature and meaningless without it becomes a sub-feature, a folder inside it: it uses its feature and everything below, and its feature and siblings never use it. One level deep. A part that stands on its own becomes a sibling feature with its own layer.
- Server: a feature is a folder and a namespace; with several concerns it splits by role (`*Endpoints` with the routes `Api/ServerApp.cs` maps, `*Store` with the SQL, the records). Types are `internal`; tests see them through `InternalsVisibleTo`.
- A record the API sends or receives is declared once in `Suwageval.Contracts` and is a contract with someone outside; a row from our own SQL is not. A store returns its own row type and the endpoint maps it, so a reader can tell whether changing a type changes the API.
- An operation's logic lives once, in the store or an operations class; the endpoint only maps it to HTTP, and a problem the caller can fix is a Problem Details whose `detail` says what to do.
- Queries are plain SQL with `@name` parameters, mapped by Dapper. No ORM and no SQL generator: the SQL a reader sees is what PostgreSQL runs.
- A closed set of values is a type of its own: an `enum` on the server, serialized by name, a union of string literals on the web.
- C# conventions on the server, the web's on the web; camelCase in JSON, snake_case tables and columns. C# tests mirror the server's folders, since they cannot sit beside the type.
