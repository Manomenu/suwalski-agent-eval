import { expect, test, type APIRequestContext, type Route } from "@playwright/test";

import { createAgent, runToEnd, serveAgent } from "../../../e2e/helpers";

async function createAgentWithTwoTags(request: APIRequestContext, name: string, endpointUrl: string) {
    const scenarios = ["smoke", "billing"].map((tag) => ({
        key: `${tag}-greets`,
        tags: [tag],
        samples: 1,
        conversation: [{ kind: "UserMessage", text: "Hello" }],
    }));
    const checks = scenarios.map((scenario) => ({
        type: "text",
        key: `${scenario.key}-says-hello`,
        scenarioKey: scenario.key,
        contains: "Hello",
    }));
    await createAgent(request, { name, endpointUrl, scenarios, checks });
}

// Why: the charts are a full screen tall and stand above the filters, so arriving later they pushed an open tag list off the screen
test("the run filters are not shown before the charts' code has arrived, and picking a tag then hides the runs limited to other tags and keeps the pick in the address", async ({
    page,
    request,
}) => {
    const name = `e2e-runs-late-charts-${String(Date.now())}`;
    const agentServer = await serveAgent({ label: "e2e-runs-late-charts" });
    try {
        await createAgentWithTwoTags(request, name, agentServer.endpointUrl);
        const smokeRun = await runToEnd(request, { agent: name, tags: ["smoke"] });
        const billingRun = await runToEnd(request, { agent: name, tags: ["billing"] });
        const runBox = (runId: string) => page.getByRole("checkbox", { name: `Select run ${runId.slice(0, 8)}`, exact: true });
        const heldCharts: Route[] = [];
        await page.route("**/src/evaluation/trend/Trends.tsx*", (route) => {
            heldCharts.push(route);
        });

        await page.goto(`/agents/${name}/evaluation`);
        await expect(page.getByLabel("Loading the charts", { exact: true })).toBeVisible();
        const tagField = page.getByRole("combobox", { name: "Filter runs by tag", exact: true });
        await expect(tagField).toHaveCount(0);

        await Promise.all(heldCharts.map((route) => route.continue()));
        await expect(page.getByRole("heading", { name: "Pass rate", exact: true })).toBeVisible();
        await expect(runBox(smokeRun)).toBeVisible();
        await expect(runBox(billingRun)).toBeVisible();
        await tagField.click();
        await tagField.fill("smo");
        await page.getByRole("option", { name: "smoke", exact: true }).click();
        await expect(runBox(billingRun)).toHaveCount(0);
        await expect(runBox(smokeRun)).toBeVisible();
        await expect(page).toHaveURL(/tag=smoke$/);
    } finally {
        agentServer.close();
    }
});

// Why: The Problems cards stand above the filters and come from requests of their own: arriving later, they pushed an open tag list away, which hid it.
test("the run filters are not shown before the Problems have arrived, so the cards never push them away", async ({ page, request }) => {
    const name = `e2e-runs-late-problems-${String(Date.now())}`;
    const agentServer = await serveAgent({ label: "e2e-runs-late-problems" });
    try {
        await createAgentWithTwoTags(request, name, agentServer.endpointUrl);
        await runToEnd(request, { agent: name, tags: ["smoke"] });
        const heldProblems: Route[] = [];
        await page.route(/\/eval\/agents\/[^/]+\/(unstable-checks|agent-errors)/, (route) => {
            heldProblems.push(route);
        });

        await page.goto(`/agents/${name}/evaluation`);
        await expect(page.getByLabel("Loading the problems", { exact: true })).toBeVisible();
        const tagField = page.getByRole("combobox", { name: "Filter runs by tag", exact: true });
        await expect(tagField).toHaveCount(0);

        await Promise.all(heldProblems.map((route) => route.continue()));
        await expect(tagField).toBeVisible();
    } finally {
        agentServer.close();
    }
});
