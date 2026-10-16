// "Re-check" on a finished run: the dialog says what would be evaluated again (nothing, while no check changed), and starting it
// opens the page of a run that says whose answers it holds.
import { createServer } from "node:http";
import type { AddressInfo } from "node:net";

import { expect, test } from "@playwright/test";

test("Re-check shows what changed, starts only then, and leads to the page of the re-check", async ({ page, request }) => {
    const agentServer = createServer((req, response) => {
        response.setHeader("content-type", "application/json");
        if (req.url === "/suwageval/contract") {
            response.end(
                JSON.stringify({ label: "e2e-recheck", agents: [{ name: "main", model: { name: "e2e-model" }, config: "Be helpful." }] }),
            );
        } else {
            response.end(JSON.stringify({ items: [{ kind: "AssistantMessage", text: "Hello there." }] }));
        }
    });
    await new Promise<void>((resolve) => agentServer.listen(0, "localhost", resolve));
    try {
        const name = `e2e-recheck-${Date.now()}`;
        const endpointUrl = `http://localhost:${(agentServer.address() as AddressInfo).port}`;
        expect((await request.post("/api/agents", { data: { name, endpointUrl } })).ok()).toBe(true);
        const scenario = { key: "greets", conversation: [{ kind: "UserMessage", text: "Hello" }] };
        expect((await request.put(`/api/agents/${name}/scenarios`, { data: { scenarios: [scenario] } })).ok()).toBe(true);
        const check = { type: "text", key: "says-hello", scenarioKey: "greets", contains: "Hello" };
        expect((await request.put(`/api/agents/${name}/checks`, { data: { checks: [check] } })).ok()).toBe(true);
        const created = await request.post("/api/runs", { data: { agent: name } });
        expect(created.ok()).toBe(true);
        const { runId } = (await created.json()) as { runId: string };
        await expect
            .poll(async () => ((await (await request.get(`/api/runs/${runId}`)).json()) as { status: string }).status, { timeout: 30_000 })
            .toBe("Done");

        // No check changed: the dialog says so and offers no way to start.
        await page.goto(`/runs/${runId}`);
        await page.getByRole("button", { name: "Re-check", exact: true }).click();
        const dialog = page.getByRole("dialog", { name: "Re-check this run?", exact: true });
        await expect(dialog.getByText("Nothing to re-check", { exact: true })).toBeVisible();
        await expect(dialog.getByRole("button", { name: "Start re-check", exact: true })).toBeHidden();
        await dialog.getByRole("button", { name: "Close", exact: true }).click();
        await expect(dialog).toBeHidden();

        // A check changed: the dialog lists it, with the change on demand.
        const changed = { ...check, contains: "there" };
        expect((await request.put(`/api/agents/${name}/checks`, { data: { checks: [changed] } })).ok()).toBe(true);
        await page.getByRole("button", { name: "Re-check", exact: true }).click();
        await expect(dialog.getByText("says-hello", { exact: true })).toBeVisible();
        await expect(dialog.getByLabel("says-hello change", { exact: true })).toHaveText("Changed");
        await dialog.getByRole("button", { name: "Show change of says-hello", exact: true }).click();
        // The change is a line diff: the run's line removed, the current one added.
        await expect(dialog.locator(".line-diff-row-removed").getByText("contains: Hello", { exact: true })).toBeVisible();
        await expect(dialog.locator(".line-diff-row-added").getByText("contains: there", { exact: true })).toBeVisible();

        await dialog.getByRole("button", { name: "Start re-check", exact: true }).click();

        await expect(page).toHaveURL(/\/runs\/[0-9a-f-]{36}$/);
        await expect(page).not.toHaveURL(`/runs/${runId}`);
        await expect(page.getByText("Re-check of", { exact: false })).toBeVisible();
        await expect(page.getByRole("link", { name: `run ${runId}`, exact: true })).toBeVisible();
    } finally {
        agentServer.close();
    }
});
