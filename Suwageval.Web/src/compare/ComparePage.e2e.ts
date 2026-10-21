// The comparison page, from both of its entries: runs ticked on the agent page (/compare?runs=…) and a group opened from the Groups
// section (/variants?group=…, the same page). The agent is a tiny HTTP server; each variant is the same server with another label,
// prompt and reply, so its first answer fails one check and its second passes both.
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
                    context: { usesRetrieval: true, retrieval: { dataVersion: "e2e-data-1", description: "e2e articles" } },
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
    const conversation = [{ kind: "UserMessage", text: "Hello" }];
    const scenarios = [
        { key: "greets", tags: ["smoke"], samples: 1, conversation },
        { key: "steady", tags: ["smoke"], samples: 1, conversation },
    ];
    expect((await request.put(`/api/agents/${name}/scenarios`, { data: { scenarios } })).ok()).toBe(true);
    const checks = [
        { type: "text", key: "says-hello", scenarioKey: "greets", contains: "Hello" },
        { type: "text", key: "mentions-refund", scenarioKey: "greets", contains: "refund" },
        { type: "text", key: "steady-hello", scenarioKey: "steady", contains: "Hello" },
    ];
    expect((await request.put(`/api/agents/${name}/checks`, { data: { checks } })).ok()).toBe(true);
}

/** Starts a run (in `group` when given) and waits until it is over; its id. */
async function run(request: APIRequestContext, agent: string, group?: string): Promise<string> {
    const created = await request.post("/api/runs", { data: { agent, group } });
    expect(created.ok()).toBe(true);
    const { runId } = (await created.json()) as { runId: string };
    await expect
        .poll(async () => ((await (await request.get(`/api/runs/${runId}`)).json()) as { status: string }).status, { timeout: 30_000 })
        .toBe("Done");
    return runId;
}

test("two runs are compared from the agent page: baseline, what differs, rows that differ and the detail of a scenario", async ({
    page,
    request,
}) => {
    const variant: Variant = { label: "e2e-before", model: "e2e-model", reply: "Hello there." };
    const agentServer = await serveAgent(variant);
    try {
        const name = `e2e-compare-${Date.now()}`;
        await createAgent(request, name, `http://localhost:${String((agentServer.address() as AddressInfo).port)}`);
        const before = await run(request, name);
        Object.assign(variant, { label: "e2e-after", reply: "Hello, your refund is on its way." });
        const after = await run(request, name);

        await page.goto(`/agents/${name}`);
        const compare = page.getByRole("button", { name: "Compare (0)", exact: true });
        await expect(compare).toBeDisabled();
        await page.getByRole("checkbox", { name: `Select run ${before.slice(0, 8)}`, exact: true }).check();
        await expect(page.getByRole("button", { name: "Compare (1)", exact: true })).toBeDisabled();
        await page.getByRole("checkbox", { name: `Select run ${after.slice(0, 8)}`, exact: true }).check();
        await page.getByRole("button", { name: "Compare (2)", exact: true }).click();

        // The older run is the baseline. Each header line says what it is: the label as the title, the version and the run as links
        // that say where they lead, the models, the tags and the data.
        await expect(page).toHaveURL(`/compare?runs=${before},${after}`);
        await expect(page.getByRole("heading", { name: `Compare runs of ${name}`, exact: true })).toBeVisible();
        await expect(page.getByText("Baseline", { exact: true })).toHaveCount(1);
        await expect(page.getByText("e2e-before", { exact: true }).first()).toBeVisible();
        await expect(page.getByRole("link", { name: `Run ${before.slice(0, 8)}`, exact: true })).toHaveAttribute("href", `/runs/${before}`);
        await expect(page.getByRole("link", { name: /^Version [0-9a-f]{8}$/ })).toHaveCount(2);
        await expect(page.getByText("Models: e2e-model", { exact: true })).toHaveCount(2);
        await expect(page.getByText("Tags: all scenarios", { exact: true })).toHaveCount(2);
        await expect(page.getByText("data e2e-data-1 · e2e articles", { exact: true })).toHaveCount(2);
        // The summary: checks, and the turn time of each run.
        await expect(page.getByText("2/3 passed · 1 failed", { exact: true })).toBeVisible();
        await expect(page.getByText("3/3 passed", { exact: true })).toBeVisible();
        await expect(page.getByText(/^median .* s · p95 .* s$/)).toHaveCount(2);
        // The tag's pass rate went from 66.7% to 100%.
        await expect(page.getByRole("row", { name: /^smoke 66.7% 100% ▲ 33.3 pp$/ })).toBeVisible();

        // What differs: the prompt (the label here) is marked changed, and its diff opens on request.
        await expect(page.getByText("Agent main · prompt", { exact: true })).toBeVisible();
        await page.getByRole("button", { name: /^Prompt changed in e2e-after: show the diff$/ }).click();
        await expect(page.getByText(/^Contract diff: e2e-before → e2e-after$/)).toBeVisible();

        // Only rows that differ is on: the scenario that came out the same is hidden until the switch is off.
        await expect(page.getByRole("button", { name: "greets", exact: true })).toBeVisible();
        await expect(page.getByRole("button", { name: "steady", exact: true })).toHaveCount(0);
        await page.getByRole("switch", { name: "Only rows that differ", exact: true }).uncheck();
        await expect(page.getByRole("button", { name: "steady", exact: true })).toBeVisible();

        // A scenario opens into the checks by run, with the reason the failed one failed, and links into the run.
        const greets = page.getByRole("button", { name: "greets", exact: true });
        await expect(greets).toHaveAttribute("aria-expanded", "false");
        await greets.click();
        await expect(greets).toHaveAttribute("aria-expanded", "true");
        const matrix = page.getByRole("table", { name: "Checks of greets by run", exact: true });
        await expect(matrix.getByText("mentions-refund", { exact: true })).toBeVisible();
        await expect(matrix.getByText("failed", { exact: true })).toHaveCount(1);
        await expect(matrix.getByText('The answer does not contain "refund".', { exact: true })).toBeVisible();
        await expect(page).toHaveURL(/open=greets/);
        await page.getByRole("link", { name: `Open in run ${before.slice(0, 8)}`, exact: true }).click();
        await expect(page).toHaveURL(`/runs/${before}#scenario-greets`);
        await expect(page.getByRole("heading", { name: "greets", exact: true })).toBeInViewport();

        // The expanded row is part of the address: the link opens it again.
        await page.goto(`/compare?runs=${before},${after}&open=greets`);
        await expect(page.getByRole("button", { name: "greets", exact: true })).toHaveAttribute("aria-expanded", "true");

        // Another run becomes the baseline by its header.
        await page.getByRole("button", { name: `Use run ${after.slice(0, 8)} as the baseline`, exact: true }).click();
        await expect(page.getByRole("row", { name: /^smoke 100% 66.7% ▼ 33.3 pp$/ })).toBeVisible();
    } finally {
        agentServer.close();
    }
});

test("a group is opened from the agent page as the same comparison, without a baseline", async ({ page, request }) => {
    const variant: Variant = { label: "e2e-small", model: "e2e-model-small", reply: "Hello there." };
    const agentServer = await serveAgent(variant);
    try {
        const suffix = String(Date.now());
        const name = `e2e-group-compare-${suffix}`;
        const group = `e2e-sweep-${suffix}`;
        await createAgent(request, name, `http://localhost:${String((agentServer.address() as AddressInfo).port)}`);
        const small = await run(request, name, group);
        Object.assign(variant, { label: "e2e-large", model: "e2e-model-large", reply: "Hello, your refund is on its way." });
        const large = await run(request, name, group);

        await page.goto(`/agents/${name}`);
        await expect(page.getByRole("heading", { name: "Groups", exact: true })).toBeVisible();
        await page.getByRole("link", { name: group, exact: true }).first().click();

        await expect(page).toHaveURL(`/variants?${new URLSearchParams({ group, agent: name }).toString()}`);
        await expect(page.getByRole("heading", { name: `Compare runs of ${name}`, exact: true })).toBeVisible();
        // A group's runs are experiments: no baseline, but every scenario is listed.
        await expect(page.getByText("Baseline", { exact: true })).toHaveCount(0);
        await expect(page.getByRole("link", { name: `Run ${small.slice(0, 8)}`, exact: true })).toBeVisible();
        await expect(page.getByRole("link", { name: `Run ${large.slice(0, 8)}`, exact: true })).toBeVisible();
        await expect(page.getByRole("link", { name: `Group ${group}`, exact: true })).toHaveCount(2);
        await expect(page.getByText("Models: e2e-model-small", { exact: true })).toBeVisible();
        await expect(page.getByText("Models: e2e-model-large", { exact: true })).toBeVisible();
        await expect(page.getByText("2/3 passed · 1 failed", { exact: true })).toBeVisible();
        await expect(page.getByText("3/3 passed", { exact: true })).toBeVisible();
        await expect(page.getByRole("row", { name: /^smoke 66.7% 100%$/ })).toBeVisible();
        // The scenario's cells say it in words, with the count behind it.
        await expect(page.getByRole("row", { name: /^greets some failed 1\/2 checks all passed 2\/2 checks$/ })).toBeVisible();
        await expect(page.getByRole("button", { name: "steady", exact: true })).toBeVisible();

        // Grouped by tag, the scenarios sit under their tag.
        await page.getByText("By tag", { exact: true }).click();
        await expect(page.getByRole("cell", { name: "smoke", exact: true })).toHaveCount(2);

        // The tag filter narrows the rows.
        await page.goto(`/variants?${new URLSearchParams({ group, agent: name, tag: "nothing" }).toString()}`);
        await expect(page.getByText("No scenario has any of the selected tags.", { exact: true })).toBeVisible();

        // Nothing spills sideways at a phone's width, a long scenario key included.
        await page.goto(`/variants?${new URLSearchParams({ group, agent: name, open: "greets" }).toString()}`);
        await page.setViewportSize({ width: 500, height: 800 });
        await expect(page.getByRole("button", { name: "greets", exact: true })).toBeVisible();
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
        expect(overflow).toBeLessThanOrEqual(0);

        // Ticked runs open the same page, by their ids; a run's page links its group to it.
        await page.setViewportSize({ width: 1280, height: 800 });
        await page.goto(`/agents/${name}?runs=all`);
        await page.getByRole("checkbox", { name: `Select run ${small.slice(0, 8)}`, exact: true }).check();
        await page.getByRole("checkbox", { name: `Select run ${large.slice(0, 8)}`, exact: true }).check();
        await page.getByRole("button", { name: "Compare variants (2)", exact: true }).click();
        await expect(page).toHaveURL(`/variants?runs=${large},${small}`.replace(`${large},${small}`, `${small},${large}`));
        await expect(page.getByRole("heading", { name: `Compare runs of ${name}`, exact: true })).toBeVisible();
        await page.goto(`/runs/${small}`);
        await page.getByRole("link", { name: group, exact: true }).click();
        await expect(page.getByRole("heading", { name: `Compare runs of ${name}`, exact: true })).toBeVisible();
    } finally {
        agentServer.close();
    }
});

test("a tag a run covered only in part has no rate there, and the page says how much of the tag ran", async ({ page, request }) => {
    const agentServer = await serveAgent({ label: "e2e-cover", model: "e2e-model", reply: "Hello there." });
    try {
        const suffix = String(Date.now());
        const name = `e2e-coverage-${suffix}`;
        const group = `e2e-cover-${suffix}`;
        await createAgent(request, name, `http://localhost:${String((agentServer.address() as AddressInfo).port)}`);
        const conversation = [{ kind: "UserMessage", text: "Hello" }];
        const scenarios = [
            { key: "greets", tags: ["smoke", "extra"], samples: 1, conversation },
            { key: "steady", tags: ["smoke"], samples: 1, conversation },
        ];
        expect((await request.put(`/api/agents/${name}/scenarios`, { data: { scenarios } })).ok()).toBe(true);
        const whole = await request.post("/api/runs", { data: { agent: name, group } });
        const partial = await request.post("/api/runs", { data: { agent: name, group, tags: ["extra"] } });
        for (const created of [whole, partial]) {
            const { runId } = (await created.json()) as { runId: string };
            await expect
                .poll(async () => ((await (await request.get(`/api/runs/${runId}`)).json()) as { status: string }).status, {
                    timeout: 30_000,
                })
                .toBe("Done");
        }

        await page.goto(`/variants?${new URLSearchParams({ group, agent: name }).toString()}`);
        await expect(page.getByRole("row", { name: /^extra 50% 50%$/ })).toBeVisible();
        await expect(page.getByRole("row", { name: /^smoke 66.7% — Not every smoke scenario ran \(1 of 2\)$/ })).toBeVisible();
    } finally {
        agentServer.close();
    }
});

test("runs that cannot be compared say why", async ({ page, request }) => {
    const agentServer = await serveAgent({ label: "e2e-one", model: "e2e-model", reply: "Hello." });
    try {
        const suffix = String(Date.now());
        const url = `http://localhost:${String((agentServer.address() as AddressInfo).port)}`;
        const first = `e2e-compare-a-${suffix}`;
        const second = `e2e-compare-b-${suffix}`;
        await createAgent(request, first, url);
        await createAgent(request, second, url);
        const ofFirst = await run(request, first, `e2e-group-${suffix}`);
        const ofSecond = await run(request, second, `e2e-group-${suffix}`);

        await page.goto(`/compare?runs=${ofFirst},${ofSecond}`);
        await expect(page.getByText("Runs of different agents cannot be shown as variants.", { exact: true })).toBeVisible();

        await page.goto(`/compare?runs=${MISSING}`);
        await expect(page.getByText(`There is no run '${MISSING}'.`, { exact: true })).toBeVisible();

        await page.goto("/variants?group=e2e-no-such-group");
        await expect(page.getByText("There is no run group 'e2e-no-such-group'.", { exact: true })).toBeVisible();

        await page.goto("/compare");
        await expect(page.getByText(/^Give the runs to show as/)).toBeVisible();
    } finally {
        agentServer.close();
    }
});
