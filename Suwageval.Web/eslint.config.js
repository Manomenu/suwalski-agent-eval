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
                // Order matters: the first matching pattern wins, so api/ is not a feature.
                { type: "api", pattern: "src/api" },
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
                            allow: { to: [{ file: { categories: "entry" } }, { element: { types: { anyOf: ["feature", "api"] } } }] },
                        },
                        // A feature uses the shared HTTP plumbing…
                        { from: { element: { type: "feature" } }, allow: { to: { element: { type: "api" } } } },
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
                        // through agents/ (versions with their model, the page's loading frame, links). The agent page
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
                        // The variants page is the compare page's sibling: the same runs and agents, the same reasons; the
                        // agent and run pages link to it by paths of their own (runs/results.ts), so the edges go one way.
                        {
                            from: { element: { type: "feature", captured: { name: "variants" } } },
                            allow: { to: { element: { type: "feature", captured: { name: "runs" } } } },
                        },
                        {
                            from: { element: { type: "feature", captured: { name: "variants" } } },
                            allow: { to: { element: { type: "feature", captured: { name: "agents" } } } },
                        },
                        // The run page and the compare table both show a check's definition, as the run used it, in a
                        // drawer: checks/ owns the button, the YAML and the syntax highlighter, runs/ and compare/ place it.
                        {
                            from: { element: { type: "feature", captured: { name: "runs" } } },
                            allow: { to: { element: { type: "feature", captured: { name: "checks" } } } },
                        },
                        {
                            from: { element: { type: "feature", captured: { name: "compare" } } },
                            allow: { to: { element: { type: "feature", captured: { name: "checks" } } } },
                        },
                        // The agent page marks its "Unstable checks" card with the icon the run page and the comparison use
                        // for an unstable check: checks/ owns the icon, agents/ places it in the heading.
                        {
                            from: { element: { type: "feature", captured: { name: "agents" } } },
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
                        {
                            from: { element: { type: "feature", captured: { name: "diff" } } },
                            allow: { to: { element: { type: "feature", captured: { name: "checks" } } } },
                        },
                        // The run, comparison and variants pages share one tag filter (the buttons, the address's `tag`
                        // parameters, which rows they keep): tagFilter/ owns it, the three pages place it.
                        ...["runs", "compare", "variants"].map((name) => ({
                            from: { element: { type: "feature", captured: { name } } },
                            allow: { to: { element: { type: "feature", captured: { name: "tagFilter" } } } },
                        })),
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
    {
        files: ["eslint.config.js"],
        extends: [tseslint.configs.disableTypeChecked],
    },
);
