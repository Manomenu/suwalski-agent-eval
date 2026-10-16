// Comparing two runs: tick them on the agent page, open the comparison, see the baseline and the switch of differences.
// The runs need an agent that answers (a tiny HTTP server): its first answer fails one check, its second passes both.
import { createServer, type Server } from "node:http";
import type { AddressInfo } from "node:net";

import { expect, test, type APIRequestContext } from "@playwright/test";

const MISSING = "00000000-0000-4000-8000-000000000000";

function serveAgent(agent: { label: string; reply: string }): Promise<Server> {
    const server = createServer((request, response) => {
        response.setHeader("content-type", "application/json");
        if (request.url === "/suwageval/contract") {
            response.end(
                JSON.stringify({
                    label: agent.label,
                    agents: [{ name: "main", model: { name: "e2e-model" }, config: agent.label }],
                    context: { usesRetrieval: true, retrieval: { dataVersion: "e2e-data-1", description: "e2e articles" } },
                }),
            );
        } else {
            response.end(JSON.stringify({ items: [{ kind: "AssistantMessage", text: agent.reply }] }));
        }
    });
    return new Promise((resolve) => server.listen(0, "localhost", () => resolve(server)));
}

/** Starts a run and waits until it is over; its id. */
async function run(request: APIRequestContext, agent: string): Promise<string> {
    const created = await request.post("/api/runs", { data: { agent } });
    expect(created.ok()).toBe(true);
    const { runId } = (await created.json()) as { runId: string };
    await expect
        .poll(async () => ((await (await request.get(`/api/runs/${runId}`)).json()) as { status: string }).status, { timeout: 30_000 })
        .toBe("Done");
    return runId;
}

test("two runs are compared from the agent page", async ({ page, request }) => {
    const agent = { label: "e2e-before", reply: "Hello there." };
    const agentServer = await serveAgent(agent);
    try {
        const name = `e2e-compare-${Date.now()}`;
        const endpointUrl = `http://localhost:${(agentServer.address() as AddressInfo).port}`;
        expect((await request.post("/api/agents", { data: { name, endpointUrl } })).ok()).toBe(true);
        const scenario = { key: "greets", tags: ["smoke"], samples: 1, conversation: [{ kind: "UserMessage", text: "Hello" }] };
        expect((await request.put(`/api/agents/${name}/scenarios`, { data: { scenarios: [scenario] } })).ok()).toBe(true);
        const checks = [
            { type: "text", key: "says-hello", scenarioKey: "greets", contains: "Hello" },
            { type: "text", key: "mentions-refund", scenarioKey: "greets", contains: "refund" },
        ];
        expect((await request.put(`/api/agents/${name}/checks`, { data: { checks } })).ok()).toBe(true);

        const before = await run(request, name);
        Object.assign(agent, { label: "e2e-after", reply: "Hello, your refund is on its way." });
        const after = await run(request, name);

        await page.goto(`/agents/${name}`);
        const compare = page.getByRole("button", { name: "Compare (0)", exact: true });
        await expect(compare).toBeDisabled();
        await page.getByRole("checkbox", { name: `Select run ${before.slice(0, 8)}`, exact: true }).check();
        await expect(page.getByRole("button", { name: "Compare (1)", exact: true })).toBeDisabled();
        await page.getByRole("checkbox", { name: `Select run ${after.slice(0, 8)}`, exact: true }).check();
        await page.getByRole("button", { name: "Compare (2)", exact: true }).click();

        // The older run is the baseline; the header of each run says its version, model and totals.
        await expect(page).toHaveURL(`/compare?runs=${before},${after}`);
        await expect(page.getByRole("heading", { name: `Compare runs of ${name}`, exact: true })).toBeVisible();
        await expect(page.getByText("Baseline", { exact: true })).toHaveCount(1);
        await expect(page.getByRole("link", { name: `Run ${before.slice(0, 8)}`, exact: true })).toBeVisible();
        await expect(page.getByText("1/2 checks passed", { exact: true })).toBeVisible();
        await expect(page.getByText("2/2 checks passed", { exact: true })).toBeVisible();
        await expect(page.getByText("e2e-model", { exact: true })).toHaveCount(2);
        // The data version and the turn time of each run.
        await expect(page.getByText("data e2e-data-1 · e2e articles", { exact: true })).toHaveCount(2);
        await expect(page.getByText(/^Turn time: median .* s · p95 .* s$/)).toHaveCount(2);
        // The tag's pass rate went from 50% to 100%.
        await expect(page.getByRole("row", { name: /^smoke 50% 100% ▲ 50 pp$/ })).toBeVisible();

        // Only differences is on: the check that did not change is hidden until the switch is off.
        await expect(page.getByText("mentions-refund", { exact: true })).toBeVisible();
        await expect(page.getByText("says-hello", { exact: true })).toHaveCount(0);
        await page.getByRole("switch", { name: "Only differences", exact: true }).uncheck();
        await expect(page.getByText("says-hello", { exact: true })).toBeVisible();

        // Another run becomes the baseline by its header.
        await page.getByRole("button", { name: `Use run ${after.slice(0, 8)} as the baseline`, exact: true }).click();
        await expect(page.getByRole("row", { name: /^smoke 50% ▼ 50 pp 100%$/ })).toBeVisible();
    } finally {
        agentServer.close();
    }
});

test("runs that cannot be compared say why", async ({ page }) => {
    await page.goto(`/compare?runs=${MISSING}`);
    await expect(page.getByText("Choose 2 to 5 runs to compare.", { exact: true })).toBeVisible();

    await page.goto(`/compare?runs=${MISSING},00000000-0000-4000-8000-000000000001`);
    await expect(page.getByText(`There is no run ${MISSING}.`, { exact: true })).toBeVisible();
});
