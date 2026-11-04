import { defineConfig, devices } from "@playwright/test";

const SERVER_PORT = Number(process.env["E2E_SERVER_PORT"] ?? 6231);
const WEB_PORT = Number(process.env["E2E_WEB_PORT"] ?? 3231);
const DATABASE = process.env["E2E_DATABASE"] ?? "suwageval_e2e";

export default defineConfig({
    testDir: "../src",
    testMatch: "**/*.e2e.ts",
    // Why: one database for the run, so tests that create data would see each other's in parallel
    workers: 1,
    // Why: the retry only tells a steady failure from a flaky one in the report; a test that passes on retry still fails the run
    retries: process.env["CI"] ? 1 : 0,
    failOnFlakyTests: Boolean(process.env["CI"]),
    outputDir: "../../.artifacts/e2e/test-results",
    reporter: process.env["CI"] ? [["list"], ["html", { open: "never", outputFolder: "../../.artifacts/e2e/report" }]] : "list",
    timeout: 60_000,
    use: {
        baseURL: `http://localhost:${WEB_PORT}`,
        locale: "pl-PL",
        trace: "retain-on-failure",
        screenshot: "only-on-failure",
    },
    projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"], viewport: { width: 1440, height: 900 } } }],
    webServer: [
        {
            command: "dotnet run --project Suwageval.Server --no-build --no-launch-profile",
            cwd: "../..",
            url: `http://localhost:${SERVER_PORT}/health`,
            env: {
                ASPNETCORE_URLS: `http://localhost:${SERVER_PORT}`,
                // Why: the worker polls the queue ten times a second because every test waits for runs and the default interval would be most of that wait
                Runs__PollInterval: "00:00:00.100",
                ConnectionStrings__Default:
                    process.env["E2E_CONNECTION_STRING"] ??
                    `Host=localhost;Port=5463;Database=${DATABASE};Username=suwageval;Password=suwageval`,
            },
            reuseExistingServer: false,
            timeout: 60_000,
        },
        {
            command: `pnpm exec vite --port ${WEB_PORT} --strictPort`,
            cwd: "..",
            url: `http://localhost:${WEB_PORT}`,
            env: { API_PROXY_TARGET: `http://localhost:${SERVER_PORT}` },
            reuseExistingServer: false,
            timeout: 60_000,
        },
    ],
});
