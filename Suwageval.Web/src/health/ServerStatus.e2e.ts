import { expect, test } from "@playwright/test";

test("the page shows that the server answers", async ({ page }) => {
    await page.goto("/");

    // Why: exact, because a substring match would also pass on the down text
    await expect(page.getByText("server: up", { exact: true })).toBeVisible();
});
