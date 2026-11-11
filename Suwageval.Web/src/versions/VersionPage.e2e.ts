import { expect, test } from "@playwright/test";

import { createAgent, runToEnd, serveAgent } from "../../e2e/helpers";

test("AC-system-tools-7: a version shows the tools of the system's code apart from its agents", async ({ page, request }) => {
    const agentServer = await serveAgent({
        label: "e2e-system-tools",
        tools: [{ name: "lookup", description: "Finds an order.", parameters: { type: "object" } }],
        context: { tools: [{ name: "book_visit", description: "Books the visit.", parameters: { type: "object" } }] },
    });
    try {
        const name = `e2e-system-tools-${String(Date.now())}`;
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl });
        const runId = await runToEnd(request, { agent: name });
        const run = (await (await request.get(`/api/eval/runs/${runId}`)).json()) as { agentVersionId: string };

        await page.goto(`/agents/${name}/versions/${run.agentVersionId}`);

        const systemTools = page.locator(".mantine-Paper-root", {
            has: page.getByRole("heading", { name: "Tools of the system's code", exact: true }),
        });
        await expect(systemTools.getByText("No agent owns these", { exact: false })).toBeVisible();
        await expect(systemTools.getByText("book_visit", { exact: true })).toBeVisible();
        await expect(systemTools.getByText("lookup", { exact: true })).toHaveCount(0);
        const agent = page.locator(".mantine-Paper-root", { has: page.getByRole("heading", { name: "Agent main", exact: true }) });
        await expect(agent.getByText("lookup", { exact: true })).toBeVisible();
        await expect(agent.getByText("book_visit", { exact: true })).toHaveCount(0);
    } finally {
        agentServer.close();
    }
});
