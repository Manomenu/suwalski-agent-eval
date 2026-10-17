// A sample where the agent gives no answer counts against no check, so the agent page lists it in an "Agent errors" card. The agent is a
// tiny HTTP server that answers 500 to one scenario in the second run only: the card then shows 1 error in 2 samples of that scenario.
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

test("a scenario where the agent gave no answer is listed on the agent page", async ({ page, request }) => {
    let failing = false;
    const agentServer = createServer((req, response) => {
        if (req.url === "/suwageval/contract") {
            response.setHeader("content-type", "application/json");
            response.end(
                JSON.stringify({ label: "e2e-errors", agents: [{ name: "main", model: { name: "e2e-model" }, config: "Be helpful." }] }),
            );
            return;
        }
        const chunks: Buffer[] = [];
        req.on("data", (chunk: Buffer) => chunks.push(chunk));
        req.on("end", () => {
            if (failing && Buffer.concat(chunks).toString().includes("Flaky")) {
                response.statusCode = 500;
                response.end("upstream exploded");
                return;
            }
            response.setHeader("content-type", "application/json");
            response.end(JSON.stringify({ items: [{ kind: "AssistantMessage", text: "Hello there." }] }));
        });
    });
    await new Promise<void>((resolve) => agentServer.listen(0, "localhost", resolve));
    try {
        const name = `e2e-errors-${Date.now()}`;
        const endpointUrl = `http://localhost:${(agentServer.address() as AddressInfo).port}`;
        expect((await request.post("/api/agents", { data: { name, endpointUrl } })).ok()).toBe(true);
        const scenarios = [
            { key: "steady", tags: ["smoke"], samples: 1, conversation: [{ kind: "UserMessage", text: "Steady" }] },
            { key: "flaky", tags: ["smoke"], samples: 1, conversation: [{ kind: "UserMessage", text: "Flaky" }] },
        ];
        expect((await request.put(`/api/agents/${name}/scenarios`, { data: { scenarios } })).ok()).toBe(true);
        const checks = scenarios.map(({ key }) => ({ type: "text", key: `says-hello-${key}`, scenarioKey: key, contains: "Hello" }));
        expect((await request.put(`/api/agents/${name}/checks`, { data: { checks } })).ok()).toBe(true);

        await finishedRun(request, name);
        await page.goto(`/agents/${name}`);
        await expect(page.getByRole("heading", { name: "Pass rate", exact: true })).toBeVisible();
        await expect(page.getByRole("heading", { name: "Agent errors", exact: true })).toHaveCount(0);

        failing = true;
        const failedRun = await finishedRun(request, name);
        await page.reload();
        await expect(page.getByRole("heading", { name: "Agent errors", exact: true })).toBeVisible();
        await expect(page.getByRole("link", { name: "flaky", exact: true })).toHaveAttribute("href", `/runs/${failedRun}`);
        await expect(page.getByText("1/2", { exact: true })).toBeVisible();
        await expect(page.getByRole("link", { name: "steady", exact: true })).toHaveCount(0);
    } finally {
        agentServer.close();
    }
});
