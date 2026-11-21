# The web app: practices

The frontend is React 19 and Vite on the shared kit [`@maniumek/suwfrontkit`](https://github.com/Manomenu/suwfrontkit) (Tailwind 4, the theme and the components) with TanStack Query v5, TypeScript strict. `src/health/` shows the pattern end to end: `api.ts`, `status.ts` with its test, `ServerStatus.tsx` with its browser test.

- **One feature, one folder**, holding its screen, hooks, logic and `api.ts`. `App.tsx` only
  puts features together; `main.tsx` only sets up the providers (the query client) and the stylesheets.
- **Types come from the server.** A feature's `api.ts` calls the kit's typed client (`api` of
  `api/client.ts`, `dataOf(api.GET("/eval/runs/{id}", …))`), typed by `api/openapi.d.ts`, and
  offers each read as a TanStack Query `queryOptions` with a key that starts with what it belongs
  to (`["agents", name, …]`, `["runs", id, …]`), so a write invalidates by prefix. No hand-written
  copies of server models, no host names: the browser always calls `/api` on its own origin (the
  Vite proxy in development, nginx in the image); the one plain `fetch` is the health probe, which is
  not in the contract.
- **Logic out of components.** Rules (parsing, formatting, state machines, saving) are
  plain TypeScript modules with a `<name>.test.ts` beside them; components and hooks only connect them to
  React. If a component needs a test of its own to cover a rule, the rule is in the wrong
  file.
- **Reads go through TanStack Query**, never an effect: `useQuery` with the query's `signal`, so
  `StrictMode`'s double mount and a changed key cancel the stale request. One-off actions (a notification, a migration of stored data) happen where code runs once,
  not in a component that may mount twice.
- **Never block the user on the network.** Typing and clicking change local state at once;
  saving happens in the background — debounced, one request in flight, retried when it
  fails — and a small status shows where it stands (saving / saved / offline), instead of a
  spinner over the form. A conflict (409) is shown to the user, never silently overwritten.
- **Errors reach the user in their language.** The kit's `dataOf` turns every failed response
  into an `ApiError`; a refusal with mistakes is shown by its title and the kit's `MistakeList`
  (each mistake with where it is, what is wrong and its fix), any other failure by its `detail`.
  No raw stack traces, no silent `catch`.
- **The kit first.** Components from `@maniumek/suwfrontkit`, layout and spacing with Tailwind
  utilities, colours only from the kit's tokens (`text-muted-foreground`, `bg-success-light`,
  `var(--ctp-mauve)`). A global stylesheet (`src/app.css`) only for a grid or an effect no utility
  says, with classes named after the feature (`.agent-layout`), not inline style objects that
  are repeated. A piece every app could use goes into the kit, not into `src/`.
- **Accessible names are the test contract.** Interactive things are real buttons, links and
  inputs with a visible label or an `aria-label` (an icon button: `aria-label="Account"`).
  That is what screen readers read and what the e2e tests find them by.
- **Chips (the kit's `Badge`) in a table cell stand one under another**, in a column (`flex flex-col items-start gap-1`), never
  side by side: `Done` above `Reference`, a name above its `Archived`. A row of chips in a narrow column wraps unevenly and is
  hard to scan down a column.
- **Numbers that shape the layout are named constants** (`CONTENT_MAX_WIDTH`), exported, and tests
  compute expectations from them — a test that hard-codes 220 breaks on a design tweak.
- **The browser's storage is for conveniences only** (a panel width, the last tab): read it
  defensively (it can be empty, full or blocked) and validate what comes back. Anything the
  user would miss lives on the server.
- **Browser tests** (`<name>.e2e.ts` beside the code) find elements by role and visible text,
  use `exact: true` (a substring match passes on the wrong text), create their own data and
  never depend on another test's. They test what a user relies on, not every button.
- **Charts** are Recharts, drawn by `charts/LineTrend.tsx` in the kit's colours; nothing else draws.

## File nesting: a file and its companions are one entry

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

- Name companions `<name>.<role>.ts` — `<name>.test.ts` (vitest), `<name>.e2e.ts` (Playwright),
  `<name>.stories.tsx` etc. — never `test_<name>.ts` or a separate a separate tests folder folder; a
  differently named file falls out of the nesting.
- Configuration nests under its owner: `Suwageval.slnx` ▸ `global.json`, `Directory.*.props`, `Directory.Build.rsp`;
  a project file ▸ its `packages.lock.json`; `package.json` ▸ lockfile,
  eslint, prettier, knip, tsconfig, vite; `appsettings.json` ▸ `appsettings.*.json`; `README.md` ▸
  the repo-level files.
- A new kind of companion or config file gets a pattern in `.vscode/settings.json` in the
  same change, rather than cluttering the explorer.
- C# tests cannot sit beside the type (they would compile into the server and its image), so
  `Suwageval.Server.Tests/` mirrors the server's folders instead — same names,
  `<Type>Tests.cs`.


## Import boundaries

`eslint.config.js` says which folder of `src/` may import which. `src/` has three kinds of folders: the areas (`evaluation/`, `monitoring/`), each a folder per feature; the shared folders (the plumbing every area uses, named in `shared`); and any other folder of `src/`, a feature of its own, independent by default, so a new folder is checked from its first line. A feature imports itself, `api/` and the shared folders; a shared folder never imports an area; the entry points at the root of `src/` (`main.tsx`, `App.tsx`) compose everything. A feature of an area imports another feature of its area only through an edge of `edgesWithinAnArea`, never one of another area. The edges and why:

| From | To | Why |
| --- | --- | --- |
| `agent` | `groups`, `trend`, `errors`, `unstable`, `runs` | the agent's Evaluation page places the run groups, the pass-rate charts, the agent errors and the unstable checks, and lists the runs with the run page's labels instead of copying them |
| `compare` | `runs` | the compare page reads runs (results, labels, times) through `runs/`; the agent page links to it by an address of `routes/`, so the edge goes one way |
| `runs`, `compare` | `checks` | `checks/` owns the icon of a model-judged check and the definition drawer, both pages place them |
| `runs` | `unstable` | the run page marks each unstable check |
| `unstable` | `checks` | the card of an unstable check sits under the icon `checks/` owns |
| `agent` | `conversations`, `checks` | the agent's Monitoring page lists its conversations and its monitoring checks with their charts |

A read goes through TanStack Query (`useQuery` with a feature's `queryOptions`); an `AbortController` of its own is refused by eslint outside `api/`. A running run is polled by `refetchInterval` of its results query, which asks the cheap summary first.
