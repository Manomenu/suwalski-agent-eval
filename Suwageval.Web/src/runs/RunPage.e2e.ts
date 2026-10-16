// A run that does not exist says so; "Run again" leads to a new run of the same agent. (Rendering of real results is
// covered by the unit tests of results.ts.) The agent is a tiny HTTP server that serves its contract.
import { createServer } from "node:http";
import type { AddressInfo } from "node:net";

import { expect, test } from "@playwright/test";

const MISSING = "00000000-0000-4000-8000-000000000000";

test("an unknown run shows that it was not found", async ({ page }) => {
    await page.goto(`/runs/${MISSING}`);

    await expect(page.getByText("Run not found", { exact: true })).toBeVisible();
    await expect(page.getByText(`There is no run ${MISSING}.`, { exact: true })).toBeVisible();
});

test("Run again starts a new run of the same agent and opens its page", async ({ page, request }) => {
    const agentServer = createServer((req, response) => {
        response.setHeader("content-type", "application/json");
        if (req.url === "/suwageval/contract") {
            response.end(JSON.stringify({ label: "e2e-rerun", systemPrompt: "Be helpful.", model: { name: "e2e-model" } }));
        } else {
            response.end(JSON.stringify({ items: [{ kind: "AssistantMessage", text: "Hello there." }] }));
        }
    });
    await new Promise<void>((resolve) => agentServer.listen(0, "localhost", resolve));
    try {
        const name = `e2e-rerun-${Date.now()}`;
        const endpointUrl = `http://localhost:${(agentServer.address() as AddressInfo).port}`;
        expect((await request.post("/api/agents", { data: { name, endpointUrl } })).ok()).toBe(true);
        const scenario = { key: "greets", tags: ["smoke"], conversation: [{ kind: "UserMessage", text: "Hello" }] };
        expect((await request.put(`/api/agents/${name}/scenarios`, { data: { scenarios: [scenario] } })).ok()).toBe(true);
        const created = await request.post("/api/runs", { data: { agent: name, tag: "smoke" } });
        expect(created.ok()).toBe(true);
        const { runId } = (await created.json()) as { runId: string };

        await page.goto(`/runs/${runId}`);
        await page.getByRole("button", { name: "Run again", exact: true }).click();

        await expect(page).not.toHaveURL(`/runs/${runId}`);
        await expect(page).toHaveURL(/\/runs\/[0-9a-f-]{36}$/);
        await expect(page.getByRole("heading", { name: `Run ${name}`, exact: true })).toBeVisible();
        await expect(page.getByText("tag smoke", { exact: false })).toBeVisible();
    } finally {
        agentServer.close();
    }
});
