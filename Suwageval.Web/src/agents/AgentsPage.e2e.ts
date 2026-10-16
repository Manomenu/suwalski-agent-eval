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
    await expect(page.getByText("Ten agent nie miał jeszcze runu.", { exact: true })).toBeVisible();
    await expect(page.getByText("Ten agent nie ma jeszcze wersji — powstaje przy pierwszym runie.", { exact: true })).toBeVisible();

    await page.getByRole("link", { name: "suwageval", exact: true }).click();
    await expect(page.getByRole("heading", { name: "Agenci", exact: true })).toBeVisible();
});

test("an unknown agent shows that it was not found", async ({ page }) => {
    await page.goto("/agents/no-such-agent");
    await expect(page.getByText("Nie ma agenta no-such-agent.", { exact: true })).toBeVisible();
});
