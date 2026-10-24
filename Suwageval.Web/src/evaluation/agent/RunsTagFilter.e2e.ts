// The runs table's tag field keeps the runs that ran the scenarios of any picked tag (and the unlimited ones), and the pick is in the address.
import { expect, test } from "@playwright/test";

import { createAgent, serveAgent, startRun } from "../../../e2e/helpers";

test("picking a tag hides the runs limited to other tags only and keeps the pick in the address", async ({ page, request }) => {
    const name = `e2e-runs-tag-${String(Date.now())}`;
    const agentServer = await serveAgent({ label: "e2e-runs-tag" });
    try {
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
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl, scenarios, checks });
        const smokeRun = await startRun(request, { agent: name, tags: ["smoke"] });
        const billingRun = await startRun(request, { agent: name, tags: ["billing"] });
        const runBox = (runId: string) => page.getByRole("checkbox", { name: `Select run ${runId.slice(0, 8)}`, exact: true });

        await page.goto(`/agents/${name}/evaluation`);
        await expect(runBox(smokeRun)).toBeVisible();
        await expect(runBox(billingRun)).toBeVisible();

        const tagField = page.getByRole("combobox", { name: "Filter runs by tag", exact: true });
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
