// The Configuration section shows what agent.yaml sets: the endpoint, the default samples and the regression settings.
import { expect, test } from "@playwright/test";

test("the agent's page shows its regression settings, and the defaults when it has none", async ({ page, request }) => {
    const stamp = Date.now();
    const configured = `e2e-config-${stamp}`;
    const plain = `e2e-config-plain-${stamp}`;
    const regression = { branches: ["master", "release"], history: 3 };
    expect(
        (
            await request.post("/api/agents", { data: { name: configured, endpointUrl: "http://configured.test", samples: 7, regression } })
        ).ok(),
    ).toBe(true);
    expect((await request.post("/api/agents", { data: { name: plain, endpointUrl: "http://plain.test" } })).ok()).toBe(true);

    await page.goto(`/agents/${configured}`);
    await expect(page.getByRole("heading", { name: "Configuration", exact: true })).toBeVisible();
    await expect(page.getByText("http://configured.test", { exact: true })).toBeVisible();
    await expect(page.getByText("master, release", { exact: true })).toBeVisible();
    await expect(page.getByText("3 reference runs", { exact: true })).toBeVisible();
    await expect(page.getByRole("row", { name: /^Samples/ }).getByText("7", { exact: true })).toBeVisible();

    await page.goto(`/agents/${plain}`);
    await expect(page.getByText("the repository's default branch", { exact: true })).toBeVisible();
    await expect(page.getByText("5 reference runs", { exact: true })).toBeVisible();
    // Both the samples and the history rows say "(default)".
    await expect(page.getByText("(default)", { exact: true })).toHaveCount(2);
    await expect(page.getByRole("row", { name: /^Samples/ }).getByText("3", { exact: true })).toBeVisible();
});
