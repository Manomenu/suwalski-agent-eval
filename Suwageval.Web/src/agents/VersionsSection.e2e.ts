// Archiving versions: select one, archive it (it disappears, and so do its runs), see it again with "Show archived", restore it.
// A version only comes from a run, and a run reads the contract the agent serves, so the test is that agent: a tiny HTTP server.
import { createServer, type Server } from "node:http";
import type { AddressInfo } from "node:net";

import { expect, test, type APIRequestContext } from "@playwright/test";

function serveContract(prompt: { current: string; label: string }): Promise<Server> {
    const server = createServer((request, response) => {
        if (request.url === "/suwageval/contract") {
            response.setHeader("content-type", "application/json");
            response.end(
                JSON.stringify({ label: prompt.label, agents: [{ name: "main", model: { name: "some-model" }, config: prompt.current }] }),
            );
        } else {
            // Not a turn the test cares about: the run fails, the version stays.
            response.statusCode = 500;
            response.end();
        }
    });
    return new Promise((resolve) => server.listen(0, "localhost", () => resolve(server)));
}

/** The rows of the Configuration section's table: endpoint, regression branches, regression history. */
const CONFIGURATION_ROWS = 3;

async function run(request: APIRequestContext, agent: string) {
    const created = await request.post("/api/runs", { data: { agent } });
    expect(created.ok()).toBe(true);
}

test("versions are archived, shown again on request and restored", async ({ page, request }) => {
    const contract = { current: "Be helpful.", label: "e2e-old" };
    const agentServer = await serveContract(contract);
    try {
        const name = `e2e-archive-${Date.now()}`;
        const endpointUrl = `http://localhost:${(agentServer.address() as AddressInfo).port}`;
        expect((await request.post("/api/agents", { data: { name, endpointUrl } })).ok()).toBe(true);
        const scenario = { key: "greets", conversation: [{ kind: "UserMessage", text: "Hello" }] };
        expect((await request.put(`/api/agents/${name}/scenarios`, { data: { scenarios: [scenario] } })).ok()).toBe(true);
        await run(request, name);
        Object.assign(contract, { current: "Be brief.", label: "e2e-new" });
        await run(request, name);

        await page.goto(`/agents/${name}`);
        const archive = page.getByRole("button", { name: "Archive selected", exact: true });
        await expect(page.getByRole("checkbox", { name: "Select version e2e-old", exact: true })).toBeVisible();
        await expect(archive).toBeDisabled();

        await page.getByRole("checkbox", { name: "Select version e2e-old", exact: true }).check();
        await expect(archive).toBeEnabled();
        await archive.click();

        await expect(page.getByRole("checkbox", { name: "Select version e2e-old", exact: true })).toHaveCount(0);
        await expect(page.getByRole("checkbox", { name: "Select version e2e-new", exact: true })).toBeVisible();
        await expect(page.getByRole("row")).toHaveCount(1 + 1 + 1 + 1 + CONFIGURATION_ROWS); // runs: header + the new version's run; versions: header + one row

        await page.getByRole("switch", { name: "Show archived", exact: true }).check();
        await expect(page.getByRole("checkbox", { name: "Select version e2e-old", exact: true })).toBeVisible();
        await expect(page.getByText("Archived", { exact: true })).toBeVisible();
        await expect(page.getByRole("row")).toHaveCount(3 + 3 + CONFIGURATION_ROWS);

        await page.getByRole("checkbox", { name: "Select version e2e-old", exact: true }).check();
        await page.getByRole("button", { name: "Unarchive selected", exact: true }).click();

        await expect(page.getByText("Archived", { exact: true })).toHaveCount(0);
        await page.getByRole("switch", { name: "Show archived", exact: true }).uncheck();
        await expect(page.getByRole("checkbox", { name: "Select version e2e-old", exact: true })).toBeVisible();
    } finally {
        agentServer.close();
    }
});
