// A run that does not exist says so; "Run again" leads to a new run of the same agent. (Rendering of real results is
// covered by the unit tests of results.ts.) The agent is a tiny HTTP server that serves its contract.
import { expect, test } from "@playwright/test";

import { createAgent, runToEnd, serveAgent, startRun } from "../../e2e/helpers";

const MISSING = "00000000-0000-4000-8000-000000000000";

test("an unknown run shows that it was not found", async ({ page }) => {
    await page.goto(`/runs/${MISSING}`);

    await expect(page.getByText("Run not found", { exact: true })).toBeVisible();
    await expect(page.getByText(`There is no run ${MISSING}.`, { exact: true })).toBeVisible();
});

test("Run again starts a new run of the same agent and opens its page", async ({ page, request }) => {
    const agentServer = await serveAgent({ label: "e2e-rerun" });
    try {
        const name = `e2e-rerun-${String(Date.now())}`;
        const scenario = { key: "greets", tags: ["smoke"], samples: 1, conversation: [{ kind: "UserMessage", text: "Hello" }] };
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl, scenarios: [scenario] });
        const runId = await startRun(request, { agent: name, tags: ["smoke", "billing"] });

        await page.goto(`/runs/${runId}`);
        await page.getByRole("button", { name: "Run again", exact: true }).click();

        await expect(page).not.toHaveURL(`/runs/${runId}`);
        await expect(page).toHaveURL(/\/runs\/[0-9a-f-]{36}$/);
        await expect(page.getByRole("heading", { name: `Run ${name}`, exact: true })).toBeVisible();
        await expect(page.getByText("tags smoke, billing", { exact: false })).toBeVisible();
    } finally {
        agentServer.close();
    }
});

test("Hide passed checks leaves the failed check, is kept in the address and survives a reload", async ({ page, request }) => {
    const agentServer = await serveAgent({ label: "e2e-hide", reply: "Hello there." });
    try {
        const name = `e2e-hide-${String(Date.now())}`;
        const scenario = { key: "greets", tags: ["smoke"], samples: 1, conversation: [{ kind: "UserMessage", text: "Hello" }] };
        const checks = [
            { type: "text", key: "says-hello", scenarioKey: "greets", contains: "Hello" },
            { type: "text", key: "mentions-refund", scenarioKey: "greets", contains: "refund" },
        ];
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl, scenarios: [scenario], checks });
        const runId = await runToEnd(request, { agent: name });

        await page.goto(`/runs/${runId}`);
        await expect(page.getByText("says-hello", { exact: true })).toBeVisible();
        await expect(page.getByText("mentions-refund", { exact: true })).toBeVisible();

        await page.getByRole("switch", { name: "Hide passed checks", exact: true }).check();
        await expect(page).toHaveURL(/hide=passed$/);
        await expect(page.getByText("says-hello", { exact: true })).toHaveCount(0);
        await expect(page.getByText("mentions-refund", { exact: true })).toBeVisible();
        await expect(
            page.getByText("Hidden: 1 passed check. Samples and Checks at the top still count the whole run.", { exact: true }),
        ).toBeVisible();

        await page.reload();
        await expect(page.getByRole("switch", { name: "Hide passed checks", exact: true })).toBeChecked();
        await expect(page.getByText("says-hello", { exact: true })).toHaveCount(0);
        await expect(page.getByText("mentions-refund", { exact: true })).toBeVisible();
    } finally {
        agentServer.close();
    }
});
