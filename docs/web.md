# The web app: practices

The frontend is React 19 with Mantine and Vite, TypeScript strict. `src/health/` shows the pattern end to end: `api.ts`, `status.ts` with its test, `ServerStatus.tsx` with its browser test.

- **One feature, one folder**, holding its screen, hooks, logic and `api.ts`. `App.tsx` only
  puts features together; `main.tsx` only sets up providers and the theme.
- **Types come from the server.** A feature's `api.ts` calls `request<T>()` from
  `api/client.ts` with `T` taken from `api/openapi.d.ts`. No hand-written copies of server
  models, no `fetch` outside `api/client.ts`, no host names: the browser always calls `/api`
  on its own origin (the Vite proxy in development, nginx in the image).
- **Logic out of components.** Rules (parsing, formatting, state machines, saving) are
  plain TypeScript modules with a `<name>.test.ts` beside them; components and hooks only connect them to
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
  inputs with a visible label or an `aria-label` (an icon button: `aria-label="Account"`).
  That is what screen readers read and what the e2e tests find them by.
- **Chips (Mantine `Badge`) in a table cell stand one under another**, in a `Stack` (`gap={4}`, `align="flex-start"`), never
  side by side: `Done` above `Reference`, a name above its `Archived`. A row of chips in a narrow column wraps unevenly and is
  hard to scan down a column.
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
- Configuration nests under its owner: `Suwageval.slnx` ▸ `global.json`, `Directory.*.props`, `Directory.Build.rsp`,
  `.csharpierrc.json`; a project file ▸ its `packages.lock.json`; `package.json` ▸ lockfile,
  eslint, prettier, knip, tsconfig, vite; `appsettings.json` ▸ `appsettings.*.json`; `README.md` ▸
  the repo-level files.
- A new kind of companion or config file gets a pattern in `.vscode/settings.json` in the
  same change, rather than cluttering the explorer.
- C# tests cannot sit beside the type (they would compile into the server and its image), so
  `Suwageval.Server.Tests/` mirrors the server's folders instead — same names,
  `<Type>Tests.cs`.

