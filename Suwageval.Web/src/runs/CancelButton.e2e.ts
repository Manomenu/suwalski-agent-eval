// "Cancel run" on a run that is going. The agent holds its answers until the test lets them go, so the run is running, with
// samples in flight, for exactly as long as the test wants: no timing race.
import { createServer } from "node:http";
import type { AddressInfo } from "node:net";

import { expect, test } from "@playwright/test";

test("Cancel run asks first, then the run ends as cancelled", async ({ page, request }) => {
    let release: () => void = () => undefined;
    const released = new Promise<void>((resolve) => {
        release = resolve;
    });
    let asked = 0;
    const agentServer = createServer((req, response) => {
        response.setHeader("content-type", "application/json");
        if (req.url === "/suwageval/contract") {
            response.end(
                JSON.stringify({ label: "e2e-cancel", agents: [{ name: "main", model: { name: "e2e-model" }, config: "Be helpful." }] }),
            );
            return;
        }
        asked++;
        void released.then(() => {
            response.end(JSON.stringify({ items: [{ kind: "AssistantMessage", text: "Hello there." }] }));
        });
    });
    await new Promise<void>((resolve) => agentServer.listen(0, "localhost", resolve));
    try {
        const name = `e2e-cancel-${Date.now()}`;
        const endpointUrl = `http://localhost:${(agentServer.address() as AddressInfo).port}`;
        expect((await request.post("/api/agents", { data: { name, endpointUrl } })).ok()).toBe(true);
        // More samples than the worker runs at once: the ones waiting never start once the run is cancelled.
        const scenario = { key: "greets", samples: 6, conversation: [{ kind: "UserMessage", text: "Hello" }] };
        expect((await request.put(`/api/agents/${name}/scenarios`, { data: { scenarios: [scenario] } })).ok()).toBe(true);
        const created = await request.post("/api/runs", { data: { agent: name } });
        expect(created.ok()).toBe(true);
        const { runId } = (await created.json()) as { runId: string };

        await page.goto(`/runs/${runId}`);
        await expect.poll(() => asked).toBeGreaterThan(0);
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
