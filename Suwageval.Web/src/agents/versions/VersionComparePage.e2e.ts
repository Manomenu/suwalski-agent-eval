// Comparing two versions: tick two on the agent page, open the comparison, read the line diff.
// A version only comes from a run, and a run reads the contract the agent serves, so the test is that agent: a tiny HTTP server.
import { expect, test } from "@playwright/test";

import { createAgent, runToEnd, serveAgent } from "../../../e2e/helpers";

const LOOKUP_TOOL = {
    name: "lookup",
    description: "Finds an order.",
    parameters: { type: "object", properties: { id: { type: "string" }, email: { type: "string" }, date: { type: "string" } } },
};

test("two ticked versions open a comparison with a line diff", async ({ page, request }) => {
    const served = {
        label: "e2e-cmp-old",
        model: "model-a",
        config: "Be helpful.\nBe brief.\nAnswer in English.\n",
        tools: [LOOKUP_TOOL],
        failing: true,
    };
    const agentServer = await serveAgent(served);
    try {
        const name = `e2e-versions-compare-${String(Date.now())}`;
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl });
        await runToEnd(request, { agent: name });
        Object.assign(served, { label: "e2e-cmp-new", model: "model-b", config: "Be helpful.\nBe kind.\nAnswer in English.\n" });
        await runToEnd(request, { agent: name });

        await page.goto(`/agents/${name}`);
        const compare = page.getByRole("button", { name: "Compare versions", exact: true });
        const oldVersion = page.getByRole("checkbox", { name: "Select version e2e-cmp-old", exact: true });
        const newVersion = page.getByRole("checkbox", { name: "Select version e2e-cmp-new", exact: true });
        await expect(oldVersion).toBeVisible();
        await expect(compare).toBeDisabled();
        await newVersion.check();
        await expect(compare).toBeDisabled();
        await oldVersion.check();
        await expect(compare).toBeEnabled();
        await compare.click();

        await expect(page.getByRole("heading", { name: "Compare versions", exact: true })).toBeVisible();
        await expect(page.locator(".line-diff-row-removed").getByText("Be brief.", { exact: true })).toBeVisible();
        await expect(page.locator(".line-diff-row-added").getByText("Be kind.", { exact: true })).toBeVisible();

        // The long unchanged tail is folded; opening it shows the lines.
        await expect(page.getByText("description: Finds an order.", { exact: true })).toHaveCount(0);
        await page.getByRole("button", { name: /unchanged lines$/ }).click();
        await expect(page.getByText("description: Finds an order.", { exact: true })).toBeVisible();
    } finally {
        agentServer.close();
    }
});
