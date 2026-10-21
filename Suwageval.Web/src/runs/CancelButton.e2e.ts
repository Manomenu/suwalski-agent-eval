// "Cancel run" on a run that is going. The agent holds its answers until the test lets them go, so the run is running, with
// samples in flight, for exactly as long as the test wants: no timing race.
import { expect, test } from "@playwright/test";

import { createAgent, serveAgent, startRun } from "../../e2e/helpers";

test("Cancel run asks first, then the run ends as cancelled", async ({ page, request }) => {
    let release: () => void = () => undefined;
    const released = new Promise<void>((resolve) => {
        release = resolve;
    });
    let asked = 0;
    const agentServer = await serveAgent({
        label: "e2e-cancel",
        beforeAnswer: () => {
            asked++;
            return released;
        },
    });
    try {
        const name = `e2e-cancel-${String(Date.now())}`;
        // More samples than the worker runs at once: the ones waiting never start once the run is cancelled.
        const scenario = { key: "greets", samples: 6, conversation: [{ kind: "UserMessage", text: "Hello" }] };
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl, scenarios: [scenario] });
        const runId = await startRun(request, { agent: name });

        await page.goto(`/runs/${runId}`);
        await expect.poll(() => asked, { intervals: [100, 250, 500] }).toBeGreaterThan(0);
        await expect(page.getByText("running", { exact: true })).toBeVisible();

        await page.getByRole("button", { name: "Cancel run", exact: true }).click();
        const dialog = page.getByRole("dialog", { name: "Cancel this run?", exact: true });
        await dialog.getByRole("button", { name: "Keep running", exact: true }).click();
        await expect(dialog).toBeHidden();
        await expect(page.getByText("running", { exact: true })).toBeVisible();

        await page.getByRole("button", { name: "Cancel run", exact: true }).click();
        await dialog.getByRole("button", { name: "Cancel run", exact: true }).click();
        await expect(page.getByText("cancelling…", { exact: true })).toBeVisible();
        await expect(page.getByRole("button", { name: "Cancel run", exact: true })).toBeHidden();

        release();
        await expect(page.getByText("cancelled", { exact: true })).toBeVisible();
        await expect(page.getByRole("button", { name: "Run again", exact: true })).toBeVisible();
        await expect(page.getByRole("button", { name: "Cancel run", exact: true })).toBeHidden();
    } finally {
        release();
        agentServer.close();
    }
});
