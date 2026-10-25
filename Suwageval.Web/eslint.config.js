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
    // src/ has three kinds of folders:
    //  - areas (src/evaluation/, later src/monitoring/): each holds the features of one product area, a folder per feature;
    //  - shared folders: plumbing and what every area uses (api/, routes/, agents/, versions/, …), listed below by name;
    //  - any other folder of src/ is a feature of its own, independent by default (the fallback, so a new folder is checked).
    // A feature of an area may import itself, the shared folders and the features of its area named in an explicit policy; never
    // another area, and a shared folder never imports an area. The web counterpart of a layer in ContractsTests.cs.
    {
        files: ["src/**/*.{ts,tsx}"],
        // Browser tests drive the page from outside; they import only e2e/helpers.
        ignores: ["src/**/*.e2e.ts"],
        plugins: { boundaries },
        settings: {
            "import/resolver": { node: { extensions: [".ts", ".tsx"] } },
            "boundaries/elements": [
                // Order matters: the first matching pattern wins.
                { type: "api", pattern: "src/api" },
                // The folders no area owns, used by every area the way api/ is: routes/ (the addresses of the pages), format/, help/,
                // tagSelect/, tagFilter/, dataVersion/, conversation/ (the items of a conversation log, shown by a run and by a monitored conversation), verdict/ (a check's verdict: its chip, message, evidence and the model-judged mark), charts/ (the line charts both areas draw), diff/, note/, pagination/, print/, shell/ (the sidebar), health/, and the two
                // pieces of an agent that more than one area shows: agents/ (the list, the delete plumbing) and versions/, archive/ (the archive plumbing both lists share) and toggle/ (ToggleChip, the one on/off control of the app).
                {
                    type: "shared",
                    pattern: [
                        "src/routes",
                        "src/format",
                        "src/help",
                        "src/tagSelect",
                        "src/tagFilter",
                        "src/dataVersion",
                        "src/conversation",
                        "src/verdict",
                        "src/charts",
                        "src/diff",
                        "src/note",
                        "src/pagination",
                        "src/print",
                        "src/shell",
                        "src/health",
                        "src/agents",
                        "src/archive",
                        "src/toggle",
                        "src/versions",
                    ],
                },
                // A feature of an area: src/<area>/<feature>. A new area is added to the braces.
                { type: "areaFeature", pattern: "src/{evaluation,monitoring}/*", capture: ["area", "name"] },
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
                                to: [
                                    { file: { categories: "entry" } },
                                    { element: { types: { anyOf: ["feature", "areaFeature", "api", "shared"] } } },
                                ],
                            },
                        },
                        // A shared folder uses the shared HTTP plumbing and the other shared folders — never an area.
                        { from: { element: { type: "shared" } }, allow: { to: { element: { types: { anyOf: ["api", "shared"] } } } } },
                        // A feature (of an area or not) uses the shared HTTP plumbing and the shared folders…
                        {
                            from: { element: { types: { anyOf: ["feature", "areaFeature"] } } },
                            allow: { to: { element: { types: { anyOf: ["api", "shared"] } } } },
                        },
                        // …and its own files, never another feature's (unless an edge is added here). An area feature never reaches
                        // into another area: the area of the target is always the area of the importer.
                        {
                            from: { element: { type: "feature" } },
                            allow: { to: { element: { type: "feature", captured: { name: "{{from.element.captured.name}}" } } } },
                        },
                        {
                            from: { element: { type: "areaFeature" } },
                            allow: {
                                to: {
                                    element: {
                                        type: "areaFeature",
                                        captured: { area: "{{from.element.captured.area}}", name: "{{from.element.captured.name}}" },
                                    },
                                },
                            },
                        },
                        // Edges between features of one area, each with the reason. `edge(from, to)` allows `from` to import `to` in
                        // the same area.
                        ...[
                            // The agent's Evaluation page places the run groups, the pass-rate charts, the agent errors and the
                            // unstable checks, and lists the agent's runs through runs/ with that feature's labels (status, progress,
                            // time) instead of copying them.
                            ["agent", "groups"],
                            ["agent", "trend"],
                            ["agent", "errors"],
                            ["agent", "unstable"],
                            ["agent", "runs"],
                            // The compare page reads runs through runs/ (their results, labels and times). The agent page links to the
                            // compare page by an address of routes/, so the edge between the two goes one way only.
                            ["compare", "runs"],
                            // The compare page marks the judge of a run with the icon of the run page's model-judged checks: checks/
                            // owns the icon and the definition drawer, compare/ and runs/ place them.
                            ["runs", "checks"],
                            ["compare", "checks"],
                            // Unstable checks have a feature of their own (the server's route, the CLI's `unstable`): the run page marks
                            // each check with the list, and the card sits under the icon of an unstable check (checks/ owns it).
                            ["runs", "unstable"],
                            ["unstable", "checks"],
                            // Monitoring: the agent's Monitoring page lists its conversations through conversations/.
                            ["agent", "conversations"],
                            // ... and shows its monitoring checks with their charts through checks/.
                            ["agent", "checks"],
                        ].map(([from, to]) => ({
                            from: { element: { type: "areaFeature", captured: { name: from } } },
                            allow: {
                                to: { element: { type: "areaFeature", captured: { area: "{{from.element.captured.area}}", name: to } } },
                            },
                        })),
                        // Example of an explicit edge — a feature's screen embedding another feature of its area: add a line above,
                        //     ["notes", "tags"],
                        // (the area is the importer's, so an edge can never cross into another area).
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
        ignores: ["src/api/**", "src/**/*.e2e.ts", "src/evaluation/runs/useRunResults.ts"],
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
