// The agent list shows an agent, its link opens its page, and the page deletes it once its name is typed.
import { expect, test } from "@playwright/test";

import { openAgentsListAt } from "../../e2e/helpers";

test("an agent is listed, opens its page and is deleted from it after its name is typed", async ({ page, request }) => {
    const name = `e2e-delete-agent-${String(Date.now())}`;
    expect((await request.post("/api/agents", { data: { name, endpointUrl: "http://localhost:9/" } })).ok()).toBe(true);

    await openAgentsListAt(page, request, name);
    await page.getByRole("link", { name, exact: true }).click();
    await expect(page.getByRole("heading", { name, exact: true })).toBeVisible();

    await page.getByRole("button", { name: "Delete agent", exact: true }).click();
    const dialog = page.getByRole("dialog", { name: "Delete this agent?", exact: true });
    await expect(
        dialog.getByText("0 versions, 0 scenarios, 0 checks, 0 runs, 0 samples and 0 verdicts will be deleted.", { exact: true }),
    ).toBeVisible();
    const confirm = dialog.getByRole("button", { name: "Delete", exact: true });
    await expect(confirm).toBeDisabled();
    const input = dialog.getByRole("textbox", { name: "Type the agent name to confirm", exact: true });
    await input.fill(name.slice(0, -1));
    await expect(confirm).toBeDisabled();
    await input.fill(name);
    await expect(confirm).toBeEnabled();
    await confirm.click();

    await expect(page.getByRole("heading", { name: "Agents", exact: true })).toBeVisible();
    await expect(page.getByRole("link", { name, exact: true })).toHaveCount(0);
    expect((await request.get(`/api/agents/${name}`)).status()).toBe(404);
});
