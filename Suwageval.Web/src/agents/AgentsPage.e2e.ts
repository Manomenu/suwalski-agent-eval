// The agent list shows an agent and its link opens its page (empty runs and versions: a
// real run needs a running agent, so their rendering is covered by the unit tests).
import { expect, test } from "@playwright/test";

test("an agent is listed and opens its page", async ({ page, request }) => {
    const name = `e2e-browse-${Date.now()}`;
    const endpointUrl = "http://localhost:9/";
    const created = await request.post("/api/agents", { data: { name, endpointUrl } });
    expect(created.ok()).toBe(true);

    await page.goto("/");
    await page.getByRole("link", { name, exact: true }).click();

    await expect(page.getByRole("heading", { name, exact: true })).toBeVisible();
    await expect(page.getByText("This agent has no runs yet.", { exact: true })).toBeVisible();
    await expect(page.getByText("This agent has no versions yet — one is created on the first run.", { exact: true })).toBeVisible();

    await page.getByRole("link", { name: "suwageval", exact: true }).click();
    await expect(page.getByRole("heading", { name: "Agents", exact: true })).toBeVisible();
});

test("an unknown agent shows that it was not found", async ({ page }) => {
    await page.goto("/agents/no-such-agent");
    await expect(page.getByText("There is no agent no-such-agent.", { exact: true })).toBeVisible();
});

test("an agent is deleted from its page after its name is typed", async ({ page, request }) => {
    const name = `e2e-delete-agent-${Date.now()}`;
    expect((await request.post("/api/agents", { data: { name, endpointUrl: "http://localhost:9/" } })).ok()).toBe(true);

    await page.goto(`/agents/${name}`);
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
