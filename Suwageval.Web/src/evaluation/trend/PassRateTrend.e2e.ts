// The "Pass rate" and "Turn time" sections of the agent page: a hint until two runs have finished, then the charts, whose tag lines
// are picked in one field above them.
import { expect, test } from "@playwright/test";

import { createAgent, runToEnd, serveAgent } from "../../../e2e/helpers";

test("the trend charts appear with the second finished run and draw only the picked tags beside All", async ({ page, request }) => {
    const agentServer = await serveAgent({ label: "e2e-trend" });
    try {
        const name = `e2e-trend-${String(Date.now())}`;
        const scenario = { key: "greets", tags: ["smoke", "billing"], samples: 1, conversation: [{ kind: "UserMessage", text: "Hello" }] };
        const check = { type: "text", key: "says-hello", scenarioKey: "greets", contains: "Hello" };
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl, scenarios: [scenario], checks: [check] });

        await runToEnd(request, { agent: name });
        await page.goto(`/agents/${name}/evaluation`);
        await expect(page.getByRole("heading", { name: "Pass rate", exact: true })).toBeVisible();
        await expect(page.getByRole("heading", { name: "Turn time", exact: true })).toBeVisible();
        await expect(page.getByText("The trend appears after two finished runs.", { exact: true })).toBeVisible();

        await runToEnd(request, { agent: name });
        await page.reload();
        await expect(page.getByText("The trend appears after two finished runs.", { exact: true })).toHaveCount(0);
        const passRate = page.locator(".mantine-Paper-root").filter({ has: page.getByRole("heading", { name: "Pass rate", exact: true }) });
        const turnTime = page.locator(".mantine-Paper-root").filter({ has: page.getByRole("heading", { name: "Turn time", exact: true }) });
        await expect(passRate.locator(".recharts-line")).toHaveCount(3);
        await expect(turnTime.locator(".recharts-line")).toHaveCount(3);
        await expect(turnTime.getByRole("radio", { name: "Median", exact: true })).toBeChecked();
        await turnTime.getByText("p95", { exact: true }).click();
        await expect(turnTime.getByRole("radio", { name: "p95", exact: true })).toBeChecked();

        await page.getByRole("combobox", { name: "Tags in the charts", exact: true }).fill("smo");
        await page.getByRole("option", { name: "smoke", exact: true }).click();
        await expect(page).toHaveURL(/series=smoke$/);
        await expect(passRate.locator(".recharts-line")).toHaveCount(2);
        await expect(turnTime.locator(".recharts-line")).toHaveCount(2);
        await expect(passRate.getByRole("list", { name: "Series" }).getByRole("listitem")).toHaveText(["All", "smoke"]);
    } finally {
        agentServer.close();
    }
});
