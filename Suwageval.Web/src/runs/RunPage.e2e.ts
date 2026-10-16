// A run that does not exist says so. (Rendering of real results is covered by
// the unit tests of results.ts: a real run needs a running agent.)
import { expect, test } from "@playwright/test";

const MISSING = "00000000-0000-4000-8000-000000000000";

test("an unknown run shows that it was not found", async ({ page }) => {
    await page.goto(`/runs/${MISSING}`);

    await expect(page.getByText("Run not found", { exact: true })).toBeVisible();
    await expect(page.getByText(`There is no run ${MISSING}.`, { exact: true })).toBeVisible();
});
