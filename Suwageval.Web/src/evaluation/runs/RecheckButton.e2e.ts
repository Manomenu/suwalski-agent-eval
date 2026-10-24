// "Re-check" on a finished run: the dialog says what would be evaluated again (nothing, while no check changed), and starting it
// opens the page of a run that says whose answers it holds.
import { expect, test } from "@playwright/test";

import { createAgent, runToEnd, serveAgent } from "../../../e2e/helpers";

test("Re-check shows what changed, starts only then, and leads to the page of the re-check", async ({ page, request }) => {
    const agentServer = await serveAgent({ label: "e2e-recheck" });
    try {
        const name = `e2e-recheck-${String(Date.now())}`;
        const check = { type: "text", key: "says-hello", scenarioKey: "greets", contains: "Hello" };
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl, checks: [check] });
        const runId = await runToEnd(request, { agent: name });

        await page.goto(`/runs/${runId}`);
        await page.getByRole("button", { name: "Re-check", exact: true }).click();
        const dialog = page.getByRole("dialog", { name: "Re-check this run?", exact: true });
        await expect(dialog.getByText("Nothing to re-check", { exact: true })).toBeVisible();
        await expect(dialog.getByRole("button", { name: "Start re-check", exact: true })).toBeHidden();
        await dialog.getByRole("button", { name: "Close", exact: true }).click();
        await expect(dialog).toBeHidden();

        const changed = { ...check, contains: "there" };
        expect((await request.put(`/api/agents/${name}/checks`, { data: { checks: [changed] } })).ok()).toBe(true);
        await page.getByRole("button", { name: "Re-check", exact: true }).click();
        await expect(dialog.getByText("says-hello", { exact: true })).toBeVisible();
        await expect(dialog.getByLabel("says-hello change", { exact: true })).toHaveText("Changed");
        await dialog.getByRole("button", { name: "Start re-check", exact: true }).click();

        await expect(page).toHaveURL(/\/runs\/[0-9a-f-]{36}$/);
        await expect(page).not.toHaveURL(`/runs/${runId}`);
        await expect(page.getByText("Re-check of", { exact: false })).toBeVisible();
        await expect(page.getByRole("link", { name: `run ${runId}`, exact: true })).toBeVisible();
    } finally {
        agentServer.close();
    }
});
