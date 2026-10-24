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

test("the sidebar shows the areas the agent's features switch on, and a switched-off area's page says how to switch it on", async ({
    page,
    request,
}) => {
    const name = `e2e-features-${String(Date.now())}`;
    const features = { evaluation: false, monitoring: { environments: ["production"] } };
    expect((await request.post("/api/agents", { data: { name, endpointUrl: "http://localhost:9/", features } })).ok()).toBe(true);
    const navbar = page.getByRole("navigation");

    await page.goto(`/agents/${name}/monitoring`);
    await expect(navbar.getByRole("link", { name: "Monitoring", exact: true })).toHaveAttribute("data-active", "true");
    await expect(navbar.getByRole("link", { name: "Evaluation", exact: true })).toHaveCount(0);
    await expect(
        page.getByText("A conversation closes after 30m without a turn · environments: production", { exact: true }),
    ).toBeVisible();

    await page.goto(`/agents/${name}/evaluation`);
    await expect(
        page.getByText("Evaluation is off for this agent: features.evaluation in its suwageval/agent.yaml (then suwageval push).", {
            exact: true,
        }),
    ).toBeVisible();
});
