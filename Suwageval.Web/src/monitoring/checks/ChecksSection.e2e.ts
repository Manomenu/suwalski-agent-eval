import { expect, test } from "@playwright/test";

test("AC-simpler-models-and-checks-7: the pushed monitoring checks are listed on the Monitoring page, with or without a description", async ({
    page,
    request,
}) => {
    const name = `e2e-monitoring-checks-${String(Date.now())}`;
    const data = { name, endpointUrl: "http://localhost:9/", features: { monitoring: {} } };
    expect((await request.post("/api/agents", { data })).ok()).toBe(true);
    const checks = [
        { key: "no-refund", type: "tool-not-called", tool: "refund_order", description: "Never refunds on its own." },
        { key: "no-wire", type: "tool-not-called", tool: "wire_money" },
    ];
    expect((await request.put(`/api/monitoring/agents/${name}/checks`, { data: { checks } })).ok()).toBe(true);

    await page.goto(`/agents/${name}/monitoring`);

    await expect(page.getByRole("cell", { name: "no-refund", exact: true })).toBeVisible();
    await expect(page.getByRole("cell", { name: "Never refunds on its own.", exact: true })).toBeVisible();
    await expect(page.getByRole("cell", { name: "no-wire", exact: true })).toBeVisible();
    await expect(page.getByText("No conversations in these days.", { exact: true })).toBeVisible();
});
