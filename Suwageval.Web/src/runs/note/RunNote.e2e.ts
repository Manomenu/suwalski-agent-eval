// A note added on the run page is saved in the background, survives a reload, and is shown in the comparison of the run with another.
import { expect, test } from "@playwright/test";

import { createAgent, runToEnd, serveAgent } from "../../../e2e/helpers";

test("a note added on the run page survives a reload and shows in the comparison", async ({ page, request }) => {
    const agentServer = await serveAgent({ label: "e2e-note", reply: "Hello there." });
    try {
        const name = `e2e-note-${String(Date.now())}`;
        const scenario = { key: "greets", tags: ["smoke"], samples: 1, conversation: [{ kind: "UserMessage", text: "Hello" }] };
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl, scenarios: [scenario] });
        const first = await runToEnd(request, { agent: name });
        const second = await runToEnd(request, { agent: name });
        const note = "rebuilt the retrieval index";

        await page.goto(`/runs/${first}`);
        await page.getByRole("button", { name: "Add note", exact: true }).click();
        await page.getByRole("textbox", { name: "Note", exact: true }).fill(note);
        await expect(page.getByRole("status")).toHaveText("Saved");

        // Done turns the box back into the text, edited later in the same place.
        await page.getByRole("button", { name: "Done", exact: true }).click();
        await page.reload();
        await expect(page.getByText(note, { exact: true })).toBeVisible();
        await page.getByRole("button", { name: "Edit note", exact: true }).click();
        await expect(page.getByRole("textbox", { name: "Note", exact: true })).toHaveValue(note);

        await page.goto(`/compare?runs=${first},${second}`);
        await expect(page.getByText(`Note: ${note}`, { exact: true }).filter({ visible: true })).toBeVisible();
    } finally {
        agentServer.close();
    }
});
