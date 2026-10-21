// The tag filter on the variants, run and comparison pages: a pressed tag narrows the scenarios and the summary to it, the address keeps
// the choice. The agent is a tiny HTTP server; the two runs differ in how the second scenario's reply answers its check.
import { createServer } from "node:http";
import type { AddressInfo } from "node:net";

import { expect, test, type APIRequestContext } from "@playwright/test";

async function run(request: APIRequestContext, agent: string): Promise<string> {
    const created = await request.post("/api/runs", { data: { agent } });
    expect(created.ok()).toBe(true);
    const { runId } = (await created.json()) as { runId: string };
    await expect
        .poll(async () => ((await (await request.get(`/api/runs/${runId}`)).json()) as { status: string }).status, { timeout: 30_000 })
        .toBe("Done");
    return runId;
}

test("a pressed tag narrows the rows and the summary on the variants, run and comparison pages", async ({ page, request }) => {
    let refunds = false;
    const agentServer = createServer((req, response) => {
        response.setHeader("content-type", "application/json");
        if (req.url === "/suwageval/contract") {
            response.end(
                JSON.stringify({
                    label: refunds ? "e2e-filter-large" : "e2e-filter-small",
                    agents: [{ name: "main", model: { name: "e2e-model" }, config: "Be helpful." }],
                }),
            );
        } else {
            response.end(
                JSON.stringify({ items: [{ kind: "AssistantMessage", text: refunds ? "Hello, a refund is coming." : "Hello." }] }),
            );
        }
    });
    await new Promise<void>((resolve) => agentServer.listen(0, "localhost", resolve));
    try {
        const suffix = String(Date.now());
        const name = `e2e-tag-filter-${suffix}`;
        const longTag = `e2e-an-extremely-long-tag-that-has-to-wrap-inside-its-button-instead-of-widening-the-page-${suffix}`;
        const endpointUrl = `http://localhost:${String((agentServer.address() as AddressInfo).port)}`;
        expect((await request.post("/api/agents", { data: { name, endpointUrl } })).ok()).toBe(true);
        const conversation = [{ kind: "UserMessage", text: "Hello" }];
        const scenarios = [
            { key: "greets", tags: ["smoke"], samples: 1, conversation },
            { key: "refunds", tags: ["billing", longTag], samples: 1, conversation },
        ];
        expect((await request.put(`/api/agents/${name}/scenarios`, { data: { scenarios } })).ok()).toBe(true);
        const checks = [
            { type: "text", key: "says-hello", scenarioKey: "greets", contains: "Hello" },
            { type: "text", key: "mentions-refund", scenarioKey: "refunds", contains: "refund" },
        ];
        expect((await request.put(`/api/agents/${name}/checks`, { data: { checks } })).ok()).toBe(true);
        const small = await run(request, name);
        refunds = true;
        const large = await run(request, name);

        // Variants: nothing is selected at first; pressing a tag narrows the scenarios and the counts of every column.
        await page.goto(`/variants?runs=${small},${large}`);
        await expect(page.getByRole("button", { name: "billing", exact: true, pressed: false })).toBeVisible();
        // Runs named one by one show the rows that differ; the scenario both runs passed comes with the switch off.
        await expect(page.getByRole("button", { name: "greets", exact: true })).toHaveCount(0);
        await page.getByRole("switch", { name: "Only rows that differ", exact: true }).uncheck();
        await expect(page.getByRole("button", { name: "greets", exact: true })).toBeVisible();
        await expect(page.getByText("1/2 passed · 1 failed", { exact: true })).toBeVisible();

        await page.getByRole("button", { name: "billing", exact: true }).click();

        await expect(page.getByRole("button", { name: "billing", exact: true, pressed: true })).toBeVisible();
        await expect(page).toHaveURL(new RegExp(`tag=billing$`));
        await expect(page.getByRole("button", { name: "greets", exact: true })).toHaveCount(0);
        await expect(page.getByRole("button", { name: "refunds", exact: true })).toBeVisible();
        await expect(page.getByRole("cell", { name: "Checks (billing)", exact: true })).toBeVisible();
        await expect(page.getByText("0/1 passed · 1 failed", { exact: true })).toBeVisible();
        await expect(page.getByText("1/1 passed", { exact: true })).toBeVisible();
        // The per-tag rows keep the selected tag only.
        await expect(page.getByRole("row", { name: /^billing 0% 100% ▲ 100 pp$/ })).toBeVisible();
        await expect(page.getByRole("row", { name: /^smoke /, exact: false })).toHaveCount(0);

        // The choice is in the address: a reload, or a shared link, shows the same view.
        await page.reload();
        await expect(page.getByRole("button", { name: "billing", exact: true, pressed: true })).toBeVisible();
        await expect(page.getByRole("button", { name: "greets", exact: true })).toHaveCount(0);

        await page.getByRole("switch", { name: "Only rows that differ", exact: true }).uncheck();
        await page.getByRole("button", { name: "Clear filter", exact: true }).click();
        await expect(page.getByRole("button", { name: "greets", exact: true })).toBeVisible();
        await expect(page).not.toHaveURL(/tag=/);

        // A long tag wraps inside its button: nothing spills sideways at a phone's width.
        await page.goto(`/variants?runs=${small},${large}&tag=${longTag}`);
        await expect(page.getByRole("button", { name: longTag, exact: true, pressed: true })).toBeVisible();
        await page.setViewportSize({ width: 500, height: 800 });
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
        expect(overflow).toBeLessThanOrEqual(0);
        await page.setViewportSize({ width: 1280, height: 800 });

        // The run page: only the scenarios with the tag, and their counts.
        await page.goto(`/runs/${small}?tag=smoke`);
        await expect(page.getByText("Checks (smoke)", { exact: true })).toBeVisible();
        await expect(page.getByText("1/1 passed", { exact: true })).toBeVisible();
        await expect(page.getByText("mentions-refund", { exact: true })).toHaveCount(0);
        await page.getByRole("button", { name: "billing", exact: true }).click();
        await expect(page.getByText("Checks (smoke, billing)", { exact: true })).toBeVisible();
        await expect(page.getByText("1/2 passed · 1 failed", { exact: true })).toBeVisible();
        await expect(page.getByText("mentions-refund", { exact: true }).first()).toBeVisible();

        // The comparison is the same page: only the scenarios of the tag, and each run's count over them.
        await page.goto(`/compare?runs=${small},${large}&tag=billing`);
        await expect(page.getByRole("button", { name: "refunds", exact: true })).toBeVisible();
        await expect(page.getByRole("button", { name: "greets", exact: true })).toHaveCount(0);
        await expect(page.getByText("0/1 passed · 1 failed", { exact: true })).toBeVisible();
        await expect(page.getByText("1/1 passed", { exact: true })).toBeVisible();
        await expect(page.getByRole("row", { name: /^smoke /, exact: false })).toHaveCount(0);
    } finally {
        agentServer.close();
    }
});
