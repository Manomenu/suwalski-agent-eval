// A check that passes in one run and fails in another of the same version is marked: a card on the agent page and an icon on
// the run page. The agent is a tiny HTTP server whose answer alternates, so the text check passes once and fails once.
import { createServer } from "node:http";
import type { AddressInfo } from "node:net";

import { expect, test, type APIRequestContext } from "@playwright/test";

async function finishedRun(request: APIRequestContext, agent: string): Promise<string> {
    const created = await request.post("/api/runs", { data: { agent } });
    expect(created.ok()).toBe(true);
    const { runId } = (await created.json()) as { runId: string };
    await expect
        .poll(async () => ((await (await request.get(`/api/runs/${runId}`)).json()) as { status: string }).status, { timeout: 30_000 })
        .toBe("Done");
    return runId;
}

test("an unstable check is listed on the agent page and marked on its run page", async ({ page, request }) => {
    let answers = 0;
    const agentServer = createServer((req, response) => {
        response.setHeader("content-type", "application/json");
        if (req.url === "/suwageval/contract") {
            response.end(
                JSON.stringify({ label: "e2e-unstable", agents: [{ name: "main", model: { name: "e2e-model" }, config: "Be helpful." }] }),
            );
        } else {
            const text = answers++ % 2 === 0 ? "Hello there." : "Goodbye.";
            response.end(JSON.stringify({ items: [{ kind: "AssistantMessage", text }] }));
        }
    });
    await new Promise<void>((resolve) => agentServer.listen(0, "localhost", resolve));
    try {
        const name = `e2e-unstable-${Date.now()}`;
        const endpointUrl = `http://localhost:${(agentServer.address() as AddressInfo).port}`;
        expect((await request.post("/api/agents", { data: { name, endpointUrl } })).ok()).toBe(true);
        const scenario = { key: "greets", tags: ["smoke"], conversation: [{ kind: "UserMessage", text: "Hello" }] };
        expect((await request.put(`/api/agents/${name}/scenarios`, { data: { scenarios: [scenario] } })).ok()).toBe(true);
        const check = { type: "text", key: "says-hello", scenarioKey: "greets", contains: "Hello" };
        expect((await request.put(`/api/agents/${name}/checks`, { data: { checks: [check] } })).ok()).toBe(true);

        await finishedRun(request, name);
        await page.goto(`/agents/${name}`);
        await expect(page.getByRole("heading", { name: "Pass rate", exact: true })).toBeVisible();
        await expect(page.getByRole("heading", { name: "Unstable checks", exact: true })).toHaveCount(0);

        const failedRun = await finishedRun(request, name);
        await page.reload();
        await expect(page.getByRole("heading", { name: "Unstable checks", exact: true })).toBeVisible();
        await expect(page.getByRole("link", { name: "greets / says-hello", exact: true })).toHaveAttribute("href", `/runs/${failedRun}`);
        await expect(page.getByText("1/2", { exact: true })).toBeVisible();

        await page.getByRole("link", { name: "greets / says-hello", exact: true }).click();
        await expect(page.getByRole("img", { name: "Unstable check", exact: true })).toBeVisible();
    } finally {
        agentServer.close();
    }
});
