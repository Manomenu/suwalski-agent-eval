import { expect, test } from "@playwright/test";

import { createAgent, runToEnd, serveAgent } from "../../../e2e/helpers";

test("AC-product-simplification-4: the Evaluation page has a Problems section with the unstable checks and the agent errors", async ({
    page,
    request,
}) => {
    const agentServer = await serveAgent({ label: "e2e-problems", failing: true });
    try {
        const name = `e2e-problems-${String(Date.now())}`;
        const scenario = { key: "greets", samples: 1, conversation: [{ kind: "UserMessage", text: "Hello" }] };
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl, scenarios: [scenario] });
        const runId = await runToEnd(request, { agent: name });
        const version = ((await (await request.get(`/api/eval/runs/${runId}`)).json()) as { agentVersionId: string }).agentVersionId;
        // Why: a real unstable check needs a version whose runs flip, which one agent double cannot be made to do on request
        const unstable = {
            scenarioKey: "greets",
            checkKey: "says-hello",
            checkRevision: 1,
            agentVersionId: version,
            runs: 2,
            passedRuns: 1,
            newestFailedRunId: runId,
        };
        await page.route(`**/api/eval/agents/${name}/unstable-checks*`, (route) => route.fulfill({ json: [unstable] }));

        await page.goto(`/agents/${name}/evaluation`);

        await expect(page.getByRole("heading", { name: "Problems", exact: true })).toBeVisible();
        await expect(page.getByRole("heading", { name: "Unstable checks", exact: true })).toBeVisible();
        await expect(page.getByRole("link", { name: "greets / says-hello", exact: true })).toBeVisible();
        await expect(page.getByRole("heading", { name: "Agent errors", exact: true })).toBeVisible();
        await expect(page.getByRole("link", { name: "greets", exact: true })).toBeVisible();
    } finally {
        agentServer.close();
    }
});
