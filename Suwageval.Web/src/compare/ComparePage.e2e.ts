// The comparison page, from both of its entries: runs ticked on the agent page and a group opened from the Groups section (the same
// page). The agent is a tiny HTTP server; the runs come in two rounds, "before" (its first answer fails one check) and "after" (it
// passes both), each round once outside a group and once in one.
import { expect, test, type APIRequestContext } from "@playwright/test";

import { createAgent, runToEnd, serveAgent, type RunningAgent, type ServedAgent } from "../../e2e/helpers";

const CONVERSATION = [{ kind: "UserMessage", text: "Hello" }];

interface Seed {
    agentServer: RunningAgent;
    name: string;
    group: string;
    plainBefore: string;
    plainAfter: string;
    groupedBefore: string;
    groupedAfter: string;
}

let seed: Seed;

test.beforeAll(async ({ request }: { request: APIRequestContext }) => {
    const served: ServedAgent = { label: "e2e-before", model: "e2e-model", reply: "Hello there." };
    const agentServer = await serveAgent(served);
    const suffix = String(Date.now());
    const name = `e2e-compare-${suffix}`;
    const group = `e2e-sweep-${suffix}`;
    const scenarios = [
        { key: "greets", tags: ["smoke"], samples: 1, conversation: CONVERSATION },
        { key: "steady", tags: ["smoke"], samples: 1, conversation: CONVERSATION },
    ];
    const checks = [
        { type: "text", key: "says-hello", scenarioKey: "greets", contains: "Hello" },
        { type: "text", key: "mentions-refund", scenarioKey: "greets", contains: "refund" },
        { type: "text", key: "steady-hello", scenarioKey: "steady", contains: "Hello" },
    ];
    await createAgent(request, { name, endpointUrl: agentServer.endpointUrl, scenarios, checks });
    const [plainBefore, groupedBefore] = await Promise.all([runToEnd(request, { agent: name }), runToEnd(request, { agent: name, group })]);
    Object.assign(served, { label: "e2e-after", reply: "Hello, your refund is on its way." });
    const [plainAfter, groupedAfter] = await Promise.all([runToEnd(request, { agent: name }), runToEnd(request, { agent: name, group })]);
    seed = { agentServer, name, group, plainBefore, plainAfter, groupedBefore, groupedAfter };
});

test.afterAll(() => {
    seed.agentServer.close();
});

test("two runs ticked on the agent page are compared against the older one, and a scenario opens into the run", async ({ page }) => {
    const { name, plainBefore, plainAfter } = seed;
    await page.goto(`/agents/${name}`);
    const compare = page.getByRole("button", { name: "Compare (0)", exact: true });
    await expect(compare).toBeDisabled();
    await page.getByRole("checkbox", { name: `Select run ${plainBefore.slice(0, 8)}`, exact: true }).check();
    await expect(page.getByRole("button", { name: "Compare (1)", exact: true })).toBeDisabled();
    await page.getByRole("checkbox", { name: `Select run ${plainAfter.slice(0, 8)}`, exact: true }).check();
    await page.getByRole("button", { name: "Compare (2)", exact: true }).click();

    await expect(page).toHaveURL(`/compare?runs=${plainBefore},${plainAfter}`);
    await expect(page.getByRole("heading", { name: `Compare runs of ${name}`, exact: true })).toBeVisible();
    await expect(page.getByText("Baseline", { exact: true })).toHaveCount(1);
    await expect(page.getByText("2/3 passed · 1 failed", { exact: true })).toBeVisible();
    await expect(page.getByText("3/3 passed", { exact: true })).toBeVisible();

    await expect(page.getByRole("button", { name: "greets", exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: "steady", exact: true })).toHaveCount(0);
    const greets = page.getByRole("button", { name: "greets", exact: true });
    await greets.click();
    await expect(greets).toHaveAttribute("aria-expanded", "true");
    const matrix = page.getByRole("table", { name: "Checks of greets by run", exact: true });
    await expect(matrix.getByText('The answer does not contain "refund".', { exact: true })).toBeVisible();
    await page.getByRole("link", { name: `Open in run ${plainBefore.slice(0, 8)}`, exact: true }).click();
    await expect(page).toHaveURL(`/runs/${plainBefore}#scenario-greets`);
    await expect(page.getByRole("heading", { name: "greets", exact: true })).toBeInViewport();
});

test("a group is opened from the agent page as the same comparison, without a baseline, and the tag choice survives a reload", async ({
    page,
}) => {
    const { name, group, groupedBefore, groupedAfter } = seed;
    await page.goto(`/agents/${name}`);
    await expect(page.getByRole("heading", { name: "Groups", exact: true })).toBeVisible();
    await page.getByRole("link", { name: group, exact: true }).first().click();

    await expect(page).toHaveURL(`/variants?${new URLSearchParams({ group, agent: name }).toString()}`);
    await expect(page.getByRole("heading", { name: `Compare runs of ${name}`, exact: true })).toBeVisible();
    await expect(page.getByText("Baseline", { exact: true })).toHaveCount(0);
    await expect(page.getByRole("link", { name: `Run ${groupedBefore.slice(0, 8)}`, exact: true })).toBeVisible();
    await expect(page.getByRole("link", { name: `Run ${groupedAfter.slice(0, 8)}`, exact: true })).toBeVisible();
    await expect(page.getByRole("row", { name: /^greets some failed 1\/2 checks all passed 2\/2 checks$/ })).toBeVisible();
    await expect(page.getByRole("button", { name: "steady", exact: true })).toBeVisible();

    await page.getByRole("button", { name: "smoke", exact: true }).click();
    await expect(page).toHaveURL(/tag=smoke$/);
    await page.reload();
    await expect(page.getByRole("button", { name: "smoke", exact: true, pressed: true })).toBeVisible();

    await page.goto(`/agents/${name}?runs=any`);
    await page.getByRole("checkbox", { name: `Select run ${groupedBefore.slice(0, 8)}`, exact: true }).check();
    await page.getByRole("checkbox", { name: `Select run ${groupedAfter.slice(0, 8)}`, exact: true }).check();
    await page.getByRole("button", { name: "Compare (2)", exact: true }).click();
    await expect(page).toHaveURL(`/compare?runs=${groupedBefore},${groupedAfter}`);
    await expect(page.getByRole("heading", { name: `Compare runs of ${name}`, exact: true })).toBeVisible();
    await expect(page.getByText("Baseline", { exact: true })).toHaveCount(0);
});
