// A note added in the comparison of a group is saved in the background and survives a reload; a comparison by run ids has no note box.
import { expect, test } from "@playwright/test";

import { createAgent, runToEnd, serveAgent } from "../../../e2e/helpers";

test("a group note added on the compare page survives a reload; a comparison by run ids has none", async ({ page, request }) => {
    const agentServer = await serveAgent({ label: "e2e-group-note", reply: "Hello there." });
    try {
        const name = `e2e-group-note-${String(Date.now())}`;
        const group = `note-${String(Date.now())}`;
        const scenario = { key: "greets", tags: ["smoke"], samples: 1, conversation: [{ kind: "UserMessage", text: "Hello" }] };
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl, scenarios: [scenario] });
        const first = await runToEnd(request, { agent: name, group });
        const second = await runToEnd(request, { agent: name, group });
        const note = "trying two models on the refund prompt";

        await page.goto(`/compare?group=${group}&agent=${name}`);
        await page.getByRole("button", { name: "Add note", exact: true }).click();
        await page.getByRole("textbox", { name: "Note", exact: true }).fill(note);
        await expect(page.getByRole("status")).toHaveText("Saved");
        await page.getByRole("button", { name: "Done", exact: true }).click();

        await page.reload();
        await expect(page.getByText(note, { exact: true })).toBeVisible();

        await page.goto(`/compare?runs=${first},${second}`);
        await expect(page.getByRole("heading", { name: `Compare runs of ${name}` })).toBeVisible();
        await expect(page.getByRole("button", { name: "Add note", exact: true })).toHaveCount(0);
        await expect(page.getByText(note, { exact: true })).toHaveCount(0);
    } finally {
        agentServer.close();
    }
});
