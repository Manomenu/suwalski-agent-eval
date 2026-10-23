// The "Pass rate" and "Turn time" sections of the agent page: a hint until two runs have finished, then the chart with a legend that toggles lines.
import { expect, test } from "@playwright/test";

import { createAgent, runToEnd, serveAgent } from "../../../e2e/helpers";

test("the pass rate chart appears with the second finished run and its legend hides a line", async ({ page, request }) => {
    const agentServer = await serveAgent({ label: "e2e-trend" });
    try {
        const name = `e2e-trend-${String(Date.now())}`;
        const scenario = { key: "greets", tags: ["smoke"], samples: 1, conversation: [{ kind: "UserMessage", text: "Hello" }] };
        const check = { type: "text", key: "says-hello", scenarioKey: "greets", contains: "Hello" };
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl, scenarios: [scenario], checks: [check] });

        await runToEnd(request, { agent: name });
        await page.goto(`/agents/${name}`);
        await expect(page.getByRole("heading", { name: "Pass rate", exact: true })).toBeVisible();
        await expect(page.getByRole("heading", { name: "Turn time", exact: true })).toBeVisible();
        await expect(page.getByText("The trend appears after two finished runs.", { exact: true })).toBeVisible();

        await runToEnd(request, { agent: name });
        await page.reload();
        await expect(page.getByText("The trend appears after two finished runs.", { exact: true })).toHaveCount(0);
        const passRate = page.locator(".mantine-Paper-root").filter({ has: page.getByRole("heading", { name: "Pass rate", exact: true }) });
        const turnTime = page.locator(".mantine-Paper-root").filter({ has: page.getByRole("heading", { name: "Turn time", exact: true }) });
        await expect(passRate.locator(".recharts-line")).toHaveCount(2);
        await expect(turnTime.locator(".recharts-line")).toHaveCount(2);
        await expect(turnTime.getByRole("radio", { name: "Median", exact: true })).toBeChecked();
        await turnTime.getByText("p95", { exact: true }).click();
        await expect(turnTime.getByRole("radio", { name: "p95", exact: true })).toBeChecked();

        const smoke = passRate.getByRole("button", { name: "smoke", exact: true });
        await expect(smoke).toHaveAttribute("aria-pressed", "true");
        await smoke.click();
        await expect(smoke).toHaveAttribute("aria-pressed", "false");
        await expect(passRate.locator(".recharts-line")).toHaveCount(1);
    } finally {
        agentServer.close();
    }
});
