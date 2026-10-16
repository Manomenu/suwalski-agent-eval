// The "Pass rate" and "Turn time" sections of the agent page: a hint until two runs have finished, then the chart with a legend that toggles lines.
import { createServer } from "node:http";
import type { AddressInfo } from "node:net";

import { expect, test, type APIRequestContext } from "@playwright/test";

async function finishedRun(request: APIRequestContext, agent: string) {
    const created = await request.post("/api/runs", { data: { agent } });
    expect(created.ok()).toBe(true);
    const { runId } = (await created.json()) as { runId: string };
    await expect
        .poll(async () => ((await (await request.get(`/api/runs/${runId}`)).json()) as { status: string }).status, { timeout: 30_000 })
        .toBe("Done");
}

test("the pass rate chart appears with the second finished run and its legend hides a line", async ({ page, request }) => {
    const agentServer = createServer((req, response) => {
        response.setHeader("content-type", "application/json");
        if (req.url === "/suwageval/contract") {
            response.end(
                JSON.stringify({ label: "e2e-trend", agents: [{ name: "main", model: { name: "e2e-model" }, config: "Be helpful." }] }),
            );
        } else {
            response.end(JSON.stringify({ items: [{ kind: "AssistantMessage", text: "Hello there." }] }));
        }
    });
    await new Promise<void>((resolve) => agentServer.listen(0, "localhost", resolve));
    try {
        const name = `e2e-trend-${Date.now()}`;
        const endpointUrl = `http://localhost:${(agentServer.address() as AddressInfo).port}`;
        expect((await request.post("/api/agents", { data: { name, endpointUrl } })).ok()).toBe(true);
        const scenario = { key: "greets", tags: ["smoke"], samples: 1, conversation: [{ kind: "UserMessage", text: "Hello" }] };
        expect((await request.put(`/api/agents/${name}/scenarios`, { data: { scenarios: [scenario] } })).ok()).toBe(true);
        const check = { type: "text", key: "says-hello", scenarioKey: "greets", contains: "Hello" };
        expect((await request.put(`/api/agents/${name}/checks`, { data: { checks: [check] } })).ok()).toBe(true);

        await finishedRun(request, name);
        await page.goto(`/agents/${name}`);
        await expect(page.getByRole("heading", { name: "Pass rate", exact: true })).toBeVisible();
        await expect(page.getByRole("heading", { name: "Turn time", exact: true })).toBeVisible();
        await expect(page.getByText("The trend appears after two finished runs.", { exact: true })).toBeVisible();

        await finishedRun(request, name);
        await page.reload();
        await expect(page.getByText("The trend appears after two finished runs.", { exact: true })).toHaveCount(0);
        const passRate = page.locator(".mantine-Paper-root").filter({ has: page.getByRole("heading", { name: "Pass rate", exact: true }) });
        const turnTime = page.locator(".mantine-Paper-root").filter({ has: page.getByRole("heading", { name: "Turn time", exact: true }) });
        await expect(passRate.locator(".recharts-line")).toHaveCount(2);
        await expect(turnTime.locator(".recharts-line")).toHaveCount(2);
        await expect(turnTime.getByRole("radio", { name: "Median", exact: true })).toBeChecked();
        await turnTime.getByText("p95", { exact: true }).click();
        await expect(turnTime.getByRole("radio", { name: "p95", exact: true })).toBeChecked();
        await expect(turnTime.locator(".recharts-line")).toHaveCount(2);

        const smoke = passRate.getByRole("button", { name: "smoke", exact: true });
        await expect(smoke).toHaveAttribute("aria-pressed", "true");
        await smoke.click();
        await expect(smoke).toHaveAttribute("aria-pressed", "false");
        await expect(passRate.locator(".recharts-line")).toHaveCount(1);
    } finally {
        agentServer.close();
    }
});
