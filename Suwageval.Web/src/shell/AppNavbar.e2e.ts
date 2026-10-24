// The sidebar leads from the agents list into an agent's pages and back, and lights the page that is open.
import { expect, test } from "@playwright/test";

test("an agent opens on Versions, Evaluation is one click away, and Agents leads back to the list", async ({ page, request }) => {
    const name = `e2e-navbar-${String(Date.now())}`;
    expect((await request.post("/api/agents", { data: { name, endpointUrl: "http://localhost:9/" } })).ok()).toBe(true);
    const navbar = page.getByRole("navigation");
    const versions = navbar.getByRole("link", { name: "Versions", exact: true });
    const evaluation = navbar.getByRole("link", { name: "Evaluation", exact: true });

    await page.goto("/");
    await page.getByRole("main").getByRole("link", { name, exact: true }).click();
    await expect(page.getByRole("heading", { name, exact: true })).toBeVisible();
    await expect(versions).toHaveAttribute("data-active", "true");
    await expect(evaluation).not.toHaveAttribute("data-active", "true");
    await expect(page.getByRole("heading", { name: "Versions", exact: true })).toBeVisible();

    await evaluation.click();
    await expect(page).toHaveURL(new RegExp(`/agents/${name}/evaluation$`));
    await expect(evaluation).toHaveAttribute("data-active", "true");
    await expect(page.getByRole("heading", { name: "Runs", exact: true })).toBeVisible();

    await navbar.getByRole("link", { name: "Agents", exact: true }).click();
    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByRole("heading", { name: "Agents", exact: true })).toBeVisible();
});
