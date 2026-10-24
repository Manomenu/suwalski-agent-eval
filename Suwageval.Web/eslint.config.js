// Lint for the web app — the counterpart of ruff on the Python side. typescript-eslint's
// strict, type-checked presets: rules that read the TypeScript types, not just the syntax.
import js from "@eslint/js";
import { defineConfig } from "eslint/config";
import boundaries from "eslint-plugin-boundaries";
import reactHooks from "eslint-plugin-react-hooks";
import tseslint from "typescript-eslint";

export default defineConfig(
    { ignores: ["dist", "src/api/openapi.d.ts"] },
    js.configs.recommended,
    tseslint.configs.strictTypeChecked,
    tseslint.configs.stylisticTypeChecked,
    reactHooks.configs.flat["recommended-latest"],
    {
        languageOptions: {
            parserOptions: { project: ["./tsconfig.app.json", "./tsconfig.node.json"], tsconfigRootDir: import.meta.dirname },
        },
        rules: {
            // Template literals with numbers are how the UI builds labels and paths.
            "@typescript-eslint/restrict-template-expressions": ["error", { allowNumber: true }],
            // `onClick={() => setOpen(false)}` is the idiomatic React handler; braces add nothing.
            "@typescript-eslint/no-confusing-void-expression": ["error", { ignoreArrowShorthand: true }],
            // A switch over a union names every member, so adding one to a type (a run status, a check kind) fails
            // here at each place that has to learn about it. A `default` does not count as handling them: it would
            // hide exactly that failure.
            "@typescript-eslint/switch-exhaustiveness-check": ["error", { considerDefaultExhaustiveForUnions: false }],
            // `const { a: _, ...rest } = obj` drops a key on purpose.
            "@typescript-eslint/no-unused-vars": ["error", { ignoreRestSiblings: true }],
        },
    },
    // Import contracts: which folder of src/ may import which (AGENTS.md, "Import contracts").
    // Every folder of src/ is a feature, and features are independent by default: a feature
    // may import only itself and api/. When one feature really composes another, that edge is
    // added below as its own policy, with a comment saying why — the web counterpart of a
    // layer in pyproject.toml.
    {
        files: ["src/**/*.{ts,tsx}"],
        // Browser tests drive the page from outside; they import only e2e/helpers.
        ignores: ["src/**/*.e2e.ts"],
        plugins: { boundaries },
        settings: {
            "import/resolver": { node: { extensions: [".ts", ".tsx"] } },
            "boundaries/elements": [
                // Order matters: the first matching pattern wins, so api/ and the shared folders are not features.
                { type: "api", pattern: "src/api" },
                // routes/ (the addresses of the pages, built and read), format/ (ids, times, shares and durations as the pages write
                // them) and help/ (a header that explains itself) are used by every feature the way api/ is: no feature owns them.
                { type: "shared", pattern: ["src/routes", "src/format", "src/help", "src/tagSelect", "src/dataVersion", "src/note"] },
                { type: "feature", pattern: "src/*", capture: ["name"] },
            ],
            // The entry points at the root of src/ (main.tsx, App.tsx) put the page together.
            "boundaries/files": [{ category: "entry", pattern: "src/*.{ts,tsx}" }],
        },
        rules: {
            "boundaries/dependencies": [
                "error",
                {
                    default: "disallow",
                    policies: [
                        // Libraries, and files inside the same folder, are free.
                        { allow: { to: { module: { origin: "external" } } } },
                        { allow: { dependency: { relationship: { to: "internal" } } } },
                        // The entry points compose everything.
                        {
                            from: { file: { categories: "entry" } },
                            allow: {
                                to: [{ file: { categories: "entry" } }, { element: { types: { anyOf: ["feature", "api", "shared"] } } }],
                            },
                        },
                        // A feature uses the shared HTTP plumbing and the shared folders…
                        { from: { element: { type: "feature" } }, allow: { to: { element: { types: { anyOf: ["api", "shared"] } } } } },
                        // …and its own files, never another feature's (unless an edge is added here).
                        {
                            from: { element: { type: "feature" } },
                            allow: { to: { element: { type: "feature", captured: { name: "{{from.element.captured.name}}" } } } },
                        },
                        // The agent page lists the agent's runs: it reads them through runs/ and shows them
                        // with that feature's labels (status, progress, time) instead of copying them.
                        {
                            from: { element: { type: "feature", captured: { name: "agents" } } },
                            allow: { to: { element: { type: "feature", captured: { name: "runs" } } } },
                        },
                        // The compare page reads runs through runs/ (their results, labels and times) and the agent
                        // through agents/ (versions with their model, links). The agent page
                        // links to the compare page by a path of its own (agents/view.ts `compareHref`), so the edge
                        // between the two goes one way only.
                        {
                            from: { element: { type: "feature", captured: { name: "compare" } } },
                            allow: { to: { element: { type: "feature", captured: { name: "runs" } } } },
                        },
                        {
                            from: { element: { type: "feature", captured: { name: "compare" } } },
                            allow: { to: { element: { type: "feature", captured: { name: "agents" } } } },
                        },
                        // The compare page marks the judge of a run with the icon of the run page's
                        // model-judged checks: checks/ owns the icon and the definition drawer, compare/ and runs/ place them.
                        {
                            from: { element: { type: "feature", captured: { name: "runs" } } },
                            allow: { to: { element: { type: "feature", captured: { name: "checks" } } } },
                        },
                        {
                            from: { element: { type: "feature", captured: { name: "compare" } } },
                            allow: { to: { element: { type: "feature", captured: { name: "checks" } } } },
                        },
                        // diff/ is the line diff of two texts: agents/ shows two versions of an agent with it, runs/ the
                        // checks a re-check changes. It wraps a long line of YAML under the start of its value the way the
                        // definition drawer does: checks/ owns that rule (`hangingIndent`).
                        {
                            from: { element: { type: "feature", captured: { name: "agents" } } },
                            allow: { to: { element: { type: "feature", captured: { name: "diff" } } } },
                        },
                        {
                            from: { element: { type: "feature", captured: { name: "runs" } } },
                            allow: { to: { element: { type: "feature", captured: { name: "diff" } } } },
                        },
                        // The comparison opens the diff of two versions' contracts under a changed prompt.
                        {
                            from: { element: { type: "feature", captured: { name: "compare" } } },
                            allow: { to: { element: { type: "feature", captured: { name: "diff" } } } },
                        },
                        {
                            from: { element: { type: "feature", captured: { name: "diff" } } },
                            allow: { to: { element: { type: "feature", captured: { name: "checks" } } } },
                        },
                        // The run and comparison pages share one tag filter (the field, the address's `tag`
                        // parameters, which rows they keep): tagFilter/ owns it, the two pages place it. The agent page's charts
                        // keep the lines they draw in the address the same way (its `series` parameter).
                        ...["runs", "compare", "agents"].map((name) => ({
                            from: { element: { type: "feature", captured: { name } } },
                            allow: { to: { element: { type: "feature", captured: { name: "tagFilter" } } } },
                        })),
                        // The run and comparison pages place the same Print button and open the print dialog the same way
                        // (`print=1` in the address): print/ owns them and print.css, the pages mark what is not printed.
                        ...["runs", "compare"].map((name) => ({
                            from: { element: { type: "feature", captured: { name } } },
                            allow: { to: { element: { type: "feature", captured: { name: "print" } } } },
                        })),
                        // Unstable checks have a feature of their own (the server's route, the CLI's `unstable`): the agent page places
                        // their card, the run page marks each check with the list. The card sits under the icon of an unstable check
                        // (checks/ owns it, runs/ places it too) and pages its rows like every list (pagination/ owns that).
                        ...["agents", "runs"].map((name) => ({
                            from: { element: { type: "feature", captured: { name } } },
                            allow: { to: { element: { type: "feature", captured: { name: "unstable" } } } },
                        })),
                        {
                            from: { element: { type: "feature", captured: { name: "unstable" } } },
                            allow: { to: { element: { type: "feature", captured: { name: "checks" } } } },
                        },
                        {
                            from: { element: { type: "feature", captured: { name: "unstable" } } },
                            allow: { to: { element: { type: "feature", captured: { name: "pagination" } } } },
                        },
                        // Every list of the agent page and the agents list pages the same way (numbered pages, the page in the
                        // address): pagination/ owns that, agents/ places it.
                        {
                            from: { element: { type: "feature", captured: { name: "agents" } } },
                            allow: { to: { element: { type: "feature", captured: { name: "pagination" } } } },
                        },
                        // Example of an explicit edge — a feature's screen embedding another feature:
                        // {
                        //     from: { element: { type: "feature", captured: { name: "notes" } } },
                        //     allow: { to: { element: { type: "feature", captured: { name: "tags" } } } },
                        // },
                    ],
                },
            ],
        },
    },
    // A request in an effect goes through api/useRequest (a signal, a key, the result ignored once aborted) — written once there.
    {
        files: ["src/**/*.{ts,tsx}"],
        // useRunResults is a polling loop (a summary every few seconds, the full results when it shows something new),
        // not one request per key: it owns its AbortController.
        ignores: ["src/api/**", "src/**/*.e2e.ts", "src/runs/useRunResults.ts"],
        rules: {
            "no-restricted-syntax": [
                "error",
                { selector: "NewExpression[callee.name='AbortController']", message: "Requests in an effect go through api/useRequest" },
            ],
        },
    },
    {
        files: ["eslint.config.js"],
        extends: [tseslint.configs.disableTypeChecked],
    },
);
