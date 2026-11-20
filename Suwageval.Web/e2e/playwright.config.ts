import { defineConfig } from "@playwright/test";
import { e2eTests } from "@maniumek/suwgate/playwright";

const SERVER_PORT = Number(process.env["E2E_SERVER_PORT"] ?? 6231);
const WEB_PORT = Number(process.env["E2E_WEB_PORT"] ?? 3231);
const DATABASE = process.env["E2E_DATABASE"] ?? "suwageval_e2e";
const RUN_QUEUE_POLLED_TEN_TIMES_A_SECOND = "00:00:00.100";

export default defineConfig(
    e2eTests({
        outputDir: "../../.artifacts/e2e/test-results",
        reporter: process.env["CI"] ? [["list"], ["html", { open: "never", outputFolder: "../../.artifacts/e2e/report" }]] : "list",
        use: { baseURL: `http://127.0.0.1:${WEB_PORT}`, locale: "pl-PL" },
        webServer: [
            {
                command: "dotnet run --project Suwageval.Server --no-build --no-launch-profile",
                cwd: "../..",
                url: `http://127.0.0.1:${SERVER_PORT}/health`,
                env: {
                    ASPNETCORE_URLS: `http://127.0.0.1:${SERVER_PORT}`,
                    Runs__PollInterval: RUN_QUEUE_POLLED_TEN_TIMES_A_SECOND,
                    ConnectionStrings__Default:
                        process.env["E2E_CONNECTION_STRING"] ??
                        `Host=127.0.0.1;Port=5463;Database=${DATABASE};Username=suwageval;Password=suwageval`,
                },
                reuseExistingServer: false,
                timeout: 60_000,
            },
            {
                command: `pnpm exec vite --host 127.0.0.1 --port ${WEB_PORT} --strictPort`,
                cwd: "..",
                url: `http://127.0.0.1:${WEB_PORT}`,
                env: { API_PROXY_TARGET: `http://127.0.0.1:${SERVER_PORT}` },
                reuseExistingServer: false,
                timeout: 60_000,
            },
        ],
    }),
);
