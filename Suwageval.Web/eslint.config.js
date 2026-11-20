import { strict } from "@maniumek/suwgate/eslint";
import { defineConfig } from "eslint/config";
import boundaries from "eslint-plugin-boundaries";
import reactHooks from "eslint-plugin-react-hooks";

const shared = [
    "routes",
    "format",
    "help",
    "tagSelect",
    "tagFilter",
    "dataVersion",
    "conversation",
    "verdict",
    "charts",
    "diff",
    "note",
    "pagination",
    "print",
    "shell",
    "health",
    "agents",
    "archive",
    "toggle",
    "versions",
];

const edgesWithinAnArea = [
    ["agent", "groups"],
    ["agent", "trend"],
    ["agent", "errors"],
    ["agent", "unstable"],
    ["agent", "runs"],
    ["compare", "runs"],
    ["runs", "checks"],
    ["compare", "checks"],
    ["runs", "unstable"],
    ["unstable", "checks"],
    ["agent", "conversations"],
    ["agent", "checks"],
];

export default defineConfig(
    strict({
        root: import.meta.dirname,
        projects: ["./tsconfig.json", "./tsconfig.node.json"],
        ignores: ["src/api/openapi.d.ts"],
        untyped: ["prettier.config.js"],
    }),
    reactHooks.configs.flat["recommended-latest"],
    {
        files: ["**/*.{ts,tsx}"],
        rules: {
            "@typescript-eslint/switch-exhaustiveness-check": ["error", { considerDefaultExhaustiveForUnions: false }],
        },
    },
    {
        files: ["src/**/*.{ts,tsx}"],
        ignores: ["src/**/*.e2e.ts"],
        plugins: { boundaries },
        settings: {
            "import/resolver": { node: { extensions: [".ts", ".tsx"] } },
            "boundaries/elements": [
                { type: "api", pattern: "src/api" },
                { type: "shared", pattern: shared.map((folder) => `src/${folder}`) },
                { type: "areaFeature", pattern: "src/{evaluation,monitoring}/*", capture: ["area", "name"] },
                { type: "feature", pattern: "src/*", capture: ["name"] },
            ],
            "boundaries/files": [{ category: "entry", pattern: "src/*.{ts,tsx}" }],
        },
        rules: {
            "boundaries/dependencies": [
                "error",
                {
                    default: "disallow",
                    policies: [
                        { allow: { to: { module: { origin: "external" } } } },
                        { allow: { dependency: { relationship: { to: "internal" } } } },
                        {
                            from: { file: { categories: "entry" } },
                            allow: {
                                to: [
                                    { file: { categories: "entry" } },
                                    { element: { types: { anyOf: ["feature", "areaFeature", "api", "shared"] } } },
                                ],
                            },
                        },
                        { from: { element: { type: "shared" } }, allow: { to: { element: { types: { anyOf: ["api", "shared"] } } } } },
                        {
                            from: { element: { types: { anyOf: ["feature", "areaFeature"] } } },
                            allow: { to: { element: { types: { anyOf: ["api", "shared"] } } } },
                        },
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
                        ...edgesWithinAnArea.map(([from, to]) => ({
                            from: { element: { type: "areaFeature", captured: { name: from } } },
                            allow: {
                                to: { element: { type: "areaFeature", captured: { area: "{{from.element.captured.area}}", name: to } } },
                            },
                        })),
                    ],
                },
            ],
        },
    },
    {
        files: ["src/**/*.{ts,tsx}"],
        ignores: ["src/api/**", "src/**/*.e2e.ts", "src/evaluation/runs/useRunResults.ts"],
        rules: {
            "no-restricted-syntax": [
                "error",
                { selector: "NewExpression[callee.name='AbortController']", message: "Requests in an effect go through api/useRequest" },
            ],
        },
    },
);
