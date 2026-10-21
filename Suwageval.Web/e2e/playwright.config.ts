// End-to-end tests: a real browser against the real server and database.
// `just e2e` (scripts/.internal/e2e.sh) prepares an empty database and runs them; CI does the
// same. They start their own server and web app on 6231/3231, so the ones you run by hand on
// 6230/3230 and your local data stay untouched.
import { defineConfig, devices } from "@playwright/test";

// e2e.sh sets these per checkout (scripts/.internal/checkout-env.sh); the defaults are the main checkout's.
const SERVER_PORT = Number(process.env["E2E_SERVER_PORT"] ?? 6231);
const WEB_PORT = Number(process.env["E2E_WEB_PORT"] ?? 3231);
const DATABASE = process.env["E2E_DATABASE"] ?? "suwageval_e2e";

export default defineConfig({
    // The tests live next to the code they check (src/App.e2e.ts by App.tsx).
    testDir: "../src",
    testMatch: "**/*.e2e.ts",
    // One database for the run, so tests that create data would see each other's in parallel.
    workers: 1,
    // On CI a failure is retried once — only to tell a steady failure from a flaky one in the
    // report. A test that passes on the retry still fails the run: flaky is a bug, in the test or
    // in the app, never a pass. Locally a failure shows at once.
    retries: process.env["CI"] ? 1 : 0,
    failOnFlakyTests: Boolean(process.env["CI"]),
    // Results (screenshots, traces, the HTML report) go where every generated file of the repo
    // goes: .artifacts/, outside git and the images.
    outputDir: "../../.artifacts/e2e/test-results",
    reporter: process.env["CI"] ? [["list"], ["html", { open: "never", outputFolder: "../../.artifacts/e2e/report" }]] : "list",
    timeout: 60_000,
    use: {
        baseURL: `http://localhost:${WEB_PORT}`,
        locale: "pl-PL",
        // What happened, step by step, when a test fails: npx playwright show-trace <file>.
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
                // The worker looks at the queue ten times a second, not once: every test waits for runs, and the default would be most of that wait.
                Runs__PollInterval: "00:00:00.100",
                ConnectionStrings__Default:
                    process.env["E2E_CONNECTION_STRING"] ??
                    `Host=localhost;Port=5463;Database=${DATABASE};Username=suwageval;Password=suwageval`,
            },
            reuseExistingServer: false,
            // e2e.sh builds the server first; this only starts it and waits for its migrations.
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
