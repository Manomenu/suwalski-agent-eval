// The comparison page, from both of its entries: runs ticked on the agent page and a group opened from the Groups section (the same
// page). It shows absolute values; a baseline is picked by a column's header and lives in the address. The agent is a tiny HTTP server; the runs come in two rounds, "before" (its first answer fails one check) and "after" (it
// passes both), each round once outside a group and once in one.
import { expect, test, type APIRequestContext } from "@playwright/test";

import { createAgent, runToEnd, serveAgent, type RunningAgent, type ServedAgent } from "../../e2e/helpers";
import { CONTENT_MAX_WIDTH } from "../shell/layout";
import { tableMinWidth } from "./layout";

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

test("two runs ticked on the agent page are shown side by side, and a scenario opens into the run", async ({ page }) => {
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
    await expect(page.getByText("Baseline", { exact: true }).filter({ visible: true })).toHaveCount(0);
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

test("a group is opened from the agent page as the same comparison, and the tag choice survives a reload", async ({ page }) => {
    const { name, group, groupedBefore, groupedAfter } = seed;
    await page.goto(`/agents/${name}`);
    await expect(page.getByRole("heading", { name: "Groups", exact: true })).toBeVisible();
    await page.getByRole("link", { name: group, exact: true }).first().click();

    await expect(page).toHaveURL(`/compare?${new URLSearchParams({ group, agent: name }).toString()}`);
    await expect(page.getByRole("heading", { name: `Compare runs of ${name}`, exact: true })).toBeVisible();
    await expect(page.getByText("Baseline", { exact: true }).filter({ visible: true })).toHaveCount(0);
    await expect(page.getByRole("link", { name: `Run ${groupedBefore.slice(0, 8)}`, exact: true })).toBeVisible();
    await expect(page.getByRole("link", { name: `Run ${groupedAfter.slice(0, 8)}`, exact: true })).toBeVisible();
    await expect(page.getByRole("row", { name: /^greets some failed 1\/2 checks all passed 2\/2 checks$/ })).toBeVisible();
    await expect(page.getByRole("button", { name: "steady", exact: true })).toBeVisible();

    const filter = page.getByRole("combobox", { name: "Filter by tag", exact: true });
    await filter.fill("smo");
    await page.getByRole("option", { name: "smoke", exact: true }).click();
    await expect(page).toHaveURL(/tag=smoke$/);
    await page.reload();
    await page.getByRole("combobox", { name: "Filter by tag", exact: true }).click();
    await expect(page.getByRole("option", { name: "smoke", exact: true, selected: true })).toBeVisible();

    await page.goto(`/agents/${name}?runs=any`);
    await page.getByRole("checkbox", { name: `Select run ${groupedBefore.slice(0, 8)}`, exact: true }).check();
    await page.getByRole("checkbox", { name: `Select run ${groupedAfter.slice(0, 8)}`, exact: true }).check();
    await page.getByRole("button", { name: "Compare (2)", exact: true }).click();
    await expect(page).toHaveURL(`/compare?runs=${groupedBefore},${groupedAfter}`);
    await expect(page.getByRole("heading", { name: `Compare runs of ${name}`, exact: true })).toBeVisible();
    await expect(page.getByText("Baseline", { exact: true }).filter({ visible: true })).toHaveCount(0);
});

test("a baseline is picked by a column's header, lives in the address and is taken back by a second click", async ({ page }) => {
    const { name, plainBefore, plainAfter } = seed;
    await page.goto(`/compare?runs=${plainBefore},${plainAfter}`);
    await expect(page.getByRole("heading", { name: `Compare runs of ${name}`, exact: true })).toBeVisible();
    await expect(page.getByText("Baseline", { exact: true }).filter({ visible: true })).toHaveCount(0);
    await expect(page.getByText(/[▲▼±].* pp$/)).toHaveCount(0);

    await page.getByRole("button", { name: `Use run ${plainBefore.slice(0, 8)} as the baseline`, exact: true }).click();
    await expect(page).toHaveURL(`/compare?runs=${plainBefore},${plainAfter}&baseline=${plainBefore}`);
    const baseline = page.getByRole("button", { name: `Run ${plainBefore.slice(0, 8)} is the baseline: click to remove it`, exact: true });
    await expect(baseline).toBeVisible();
    await expect(page.getByText("Baseline", { exact: true }).filter({ visible: true })).toHaveCount(1);
    await expect(page.getByText(/▲ \d+(\.\d+)? pp/).first()).toBeVisible();

    await page.getByRole("button", { name: `Use run ${plainAfter.slice(0, 8)} as the baseline`, exact: true }).click();
    await expect(page).toHaveURL(`/compare?runs=${plainBefore},${plainAfter}&baseline=${plainAfter}`);

    await page.getByRole("button", { name: `Run ${plainAfter.slice(0, 8)} is the baseline: click to remove it`, exact: true }).click();
    await expect(page).toHaveURL(`/compare?runs=${plainBefore},${plainAfter}`);
    await expect(page.getByText("Baseline", { exact: true }).filter({ visible: true })).toHaveCount(0);
    await expect(page.getByText(/[▲▼±].* pp$/)).toHaveCount(0);
});

test("with a baseline every cell is read by its change, and no cell is marked best", async ({ page }) => {
    const { name, plainBefore, plainAfter } = seed;
    await page.goto(`/compare?runs=${plainBefore},${plainAfter}`);
    await expect(page.getByRole("heading", { name: `Compare runs of ${name}`, exact: true })).toBeVisible();
    await expect(page.locator("[data-change]")).toHaveCount(0);

    await page.getByRole("button", { name: `Use run ${plainBefore.slice(0, 8)} as the baseline`, exact: true }).click();
    const greets = page.getByRole("row", { name: /^greets /, exact: false });
    await expect(greets.locator('td[data-change="better"]')).toHaveCount(1);
    await expect(greets.getByText("better than the baseline", { exact: true })).toHaveCount(1);
    const tagRow = page.getByRole("row", { name: /^smoke /, exact: false });
    await expect(tagRow.locator('td[data-change="better"]')).toHaveCount(1);
    await expect(page.locator('td[data-change="worse"]')).toHaveCount(0);
    await expect(page.locator("td[style*='font-weight: 700']")).toHaveCount(0);

    await page.getByRole("button", { name: `Use run ${plainAfter.slice(0, 8)} as the baseline`, exact: true }).click();
    await expect(page.locator('td[data-change="worse"]').first()).toBeVisible();
    await expect(page.locator('td[data-change="better"]')).toHaveCount(0);
});

test("a baseline in the address of a group is read at once", async ({ page }) => {
    const { name, group, groupedBefore } = seed;
    await page.goto(`/compare?${new URLSearchParams({ group, agent: name, baseline: groupedBefore }).toString()}`);
    await expect(page.getByText("Baseline", { exact: true }).filter({ visible: true })).toHaveCount(1);
});

test("the comparison takes the whole width of a wide screen, and its table scrolls sideways on a narrow one", async ({ page }) => {
    const { name, group } = seed;
    const address = `/compare?${new URLSearchParams({ group, agent: name }).toString()}`;
    const table = page.getByRole("table").first();

    await page.setViewportSize({ width: CONTENT_MAX_WIDTH + 1000, height: 900 });
    await page.goto(address);
    await expect(page.getByRole("heading", { name: `Compare runs of ${name}`, exact: true })).toBeVisible();
    expect((await table.boundingBox())?.width).toBeGreaterThan(CONTENT_MAX_WIDTH);

    await page.setViewportSize({ width: tableMinWidth(2) - 100, height: 900 });
    const scroller = page.locator(".mantine-ScrollArea-viewport, .mantine-Table-scrollContainer").filter({ has: table }).first();
    await expect.poll(() => scroller.evaluate((element) => element.scrollWidth > element.clientWidth)).toBe(true);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});

test("a prompt's diff reads from the baseline's version, whichever column the baseline is", async ({ page, request }) => {
    // Three versions, oldest first; the baseline is the third and newest, so the server (which puts the older version first) gives
    // each diff the other way round, and the page turns it back.
    const served: ServedAgent = { label: "p1", config: "Prompt one.", reply: "Hello." };
    const agentServer = await serveAgent(served);
    try {
        const name = `e2e-diff-${String(Date.now())}`;
        const scenarios = [{ key: "greets", samples: 1, conversation: CONVERSATION }];
        const checks = [{ type: "text", key: "says-hello", scenarioKey: "greets", contains: "Hello" }];
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl, scenarios, checks });
        const first = await runToEnd(request, { agent: name });
        Object.assign(served, { label: "p2", config: "Prompt two." });
        const second = await runToEnd(request, { agent: name });
        Object.assign(served, { label: "p3", config: "Prompt three." });
        const third = await runToEnd(request, { agent: name });

        await page.goto(`/compare?runs=${first},${second},${third}&baseline=${third}`);
        await page.getByRole("button", { name: "Prompt changed in p1: show the diff", exact: true }).click();

        // The diff of p1's column only (p2's is in the page too, folded until it is opened or printed).
        const diff = page.getByRole("row").filter({ hasText: "Contract diff: p3 → p1" });
        await expect(diff).toBeVisible();
        await expect(diff.locator(".line-diff-row-removed", { hasText: "Prompt three." })).toBeVisible();
        await expect(diff.locator(".line-diff-row-added", { hasText: "Prompt one." })).toBeVisible();
    } finally {
        agentServer.close();
    }
});
