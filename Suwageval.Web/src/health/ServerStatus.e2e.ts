// The page reaches the server through the same /api path the cluster uses.
import { expect, test } from "@playwright/test";

test("the page shows that the server answers", async ({ page }) => {
    await page.goto("/");

    // exact: a substring match would also pass on "down (…)".
    await expect(page.getByText("server: up", { exact: true })).toBeVisible();
});
