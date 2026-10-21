// Comparing variants: runs started in one group, opened from the agent page's Groups section, from a run and from ticked runs.
// The agent is a tiny HTTP server; each variant is the same server with another label, model and reply.
import { createServer, type Server } from "node:http";
import type { AddressInfo } from "node:net";

import { expect, test, type APIRequestContext } from "@playwright/test";

const MISSING = "00000000-0000-4000-8000-000000000000";

interface Variant {
    label: string;
    model: string;
    reply: string;
}

function serveAgent(variant: Variant): Promise<Server> {
    const server = createServer((request, response) => {
        response.setHeader("content-type", "application/json");
        if (request.url === "/suwageval/contract") {
            response.end(
                JSON.stringify({
                    label: variant.label,
                    agents: [{ name: "main", model: { name: variant.model }, config: variant.label }],
                    context: { usesRetrieval: false },
                }),
            );
        } else {
            response.end(JSON.stringify({ items: [{ kind: "AssistantMessage", text: variant.reply }] }));
        }
    });
    return new Promise((resolve) => server.listen(0, "localhost", () => resolve(server)));
}

async function createAgent(request: APIRequestContext, name: string, endpointUrl: string) {
    expect((await request.post("/api/agents", { data: { name, endpointUrl } })).ok()).toBe(true);
    const scenario = { key: "greets", tags: ["smoke"], samples: 1, conversation: [{ kind: "UserMessage", text: "Hello" }] };
    expect((await request.put(`/api/agents/${name}/scenarios`, { data: { scenarios: [scenario] } })).ok()).toBe(true);
    const checks = [
        { type: "text", key: "says-hello", scenarioKey: "greets", contains: "Hello" },
        { type: "text", key: "mentions-refund", scenarioKey: "greets", contains: "refund" },
    ];
    expect((await request.put(`/api/agents/${name}/checks`, { data: { checks } })).ok()).toBe(true);
}

/** Starts a run in `group` and waits until it is over; its id. */
async function run(request: APIRequestContext, agent: string, group: string): Promise<string> {
    const created = await request.post("/api/runs", { data: { agent, group } });
    expect(created.ok()).toBe(true);
    const { runId } = (await created.json()) as { runId: string };
    await expect
        .poll(async () => ((await (await request.get(`/api/runs/${runId}`)).json()) as { status: string }).status, { timeout: 30_000 })
        .toBe("Done");
    return runId;
}

test("a group is opened from the agent page and shows its runs as variants", async ({ page, request }) => {
    const variant: Variant = { label: "e2e-small", model: "e2e-model-small", reply: "Hello there." };
    const agentServer = await serveAgent(variant);
    try {
        const suffix = String(Date.now());
        const name = `e2e-variants-${suffix}`;
        const group = `e2e-sweep-${suffix}`;
        await createAgent(request, name, `http://localhost:${String((agentServer.address() as AddressInfo).port)}`);
        const small = await run(request, name, group);
        Object.assign(variant, { label: "e2e-large", model: "e2e-model-large", reply: "Hello, your refund is on its way." });
        const large = await run(request, name, group);

        // The agent page lists the group; its link opens the variants.
        await page.goto(`/agents/${name}`);
        await expect(page.getByRole("heading", { name: "Groups", exact: true })).toBeVisible();
        await page.getByRole("link", { name: group, exact: true }).first().click();

        await expect(page).toHaveURL(`/variants?${new URLSearchParams({ group, agent: name }).toString()}`);
        await expect(page.getByRole("heading", { name: `Variants of ${name}`, exact: true })).toBeVisible();
        // A column per run, named by its version's label, with the models and the totals.
        await expect(page.getByRole("link", { name: "e2e-small", exact: true })).toHaveAttribute("href", `/runs/${small}`);
        await expect(page.getByRole("link", { name: "e2e-large", exact: true })).toHaveAttribute("href", `/runs/${large}`);
        await expect(page.getByText("e2e-model-small", { exact: true })).toBeVisible();
        await expect(page.getByText("e2e-model-large", { exact: true })).toBeVisible();
        await expect(page.getByText("1/2 passed · 1 failed", { exact: true })).toBeVisible();
        await expect(page.getByText("2/2 passed", { exact: true })).toBeVisible();
        // The tag's pass rate and the scenario's checks, with the failed one named.
        await expect(page.getByRole("row", { name: /^smoke 50% 100%$/ })).toBeVisible();
        await expect(page.getByRole("row", { name: /^greets 1\/2 checks failed: mentions-refund 2\/2 checks$/ })).toBeVisible();

        // Grouped by tag, the scenario sits under its tag.
        await page.getByText("By tag", { exact: true }).click();
        await expect(page.getByRole("cell", { name: "smoke", exact: true })).toHaveCount(2);

        // Nothing spills sideways at a phone's width.
        await page.setViewportSize({ width: 500, height: 800 });
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
        expect(overflow).toBeLessThanOrEqual(0);

        // The runs list: a run's group opens the same page, and ticked runs open their variants.
        await page.setViewportSize({ width: 1280, height: 800 });
        await page.goto(`/agents/${name}`);
        await page.getByRole("checkbox", { name: `Select run ${small.slice(0, 8)}`, exact: true }).check();
        await page.getByRole("checkbox", { name: `Select run ${large.slice(0, 8)}`, exact: true }).check();
        await page.getByRole("button", { name: "Compare variants (2)", exact: true }).click();
        await expect(page).toHaveURL(`/variants?runs=${small},${large}`);
        await expect(page.getByRole("link", { name: "e2e-small", exact: true })).toBeVisible();

        // A run's page links its group to the variants too.
        await page.goto(`/runs/${small}`);
        await page.getByRole("link", { name: group, exact: true }).click();
        await expect(page.getByRole("heading", { name: `Variants of ${name}`, exact: true })).toBeVisible();
    } finally {
        agentServer.close();
    }
});

test("runs that cannot be shown as variants say why", async ({ page, request }) => {
    const agentServer = await serveAgent({ label: "e2e-one", model: "e2e-model", reply: "Hello." });
    try {
        const suffix = String(Date.now());
        const url = `http://localhost:${String((agentServer.address() as AddressInfo).port)}`;
        const first = `e2e-variants-a-${suffix}`;
        const second = `e2e-variants-b-${suffix}`;
        await createAgent(request, first, url);
        await createAgent(request, second, url);
        const ofFirst = await run(request, first, `e2e-group-${suffix}`);
        const ofSecond = await run(request, second, `e2e-group-${suffix}`);

        await page.goto(`/variants?runs=${ofFirst},${ofSecond}`);
        await expect(page.getByText("Runs of different agents cannot be shown as variants.", { exact: true })).toBeVisible();

        await page.goto(`/variants?runs=${MISSING}`);
        await expect(page.getByText(`There is no run '${MISSING}'.`, { exact: true })).toBeVisible();

        await page.goto("/variants?group=e2e-no-such-group");
        await expect(page.getByText("There is no run group 'e2e-no-such-group'.", { exact: true })).toBeVisible();

        await page.goto("/variants");
        await expect(page.getByText(/^Give the runs to show as/)).toBeVisible();
    } finally {
        agentServer.close();
    }
});
