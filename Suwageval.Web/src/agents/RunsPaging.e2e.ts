// An agent with more runs than one page: the page shows PAGE_SIZE of them (the newest), "Load more" adds the rest and goes away,
// and a ticked run stays ticked. The agent is a tiny HTTP server: a run reads its contract, and its turns fail (the runs still exist).
import { createServer, type Server } from "node:http";
import type { AddressInfo } from "node:net";

import { expect, test } from "@playwright/test";

import { PAGE_SIZE } from "../runs/paging";

const EXTRA_RUNS = 3;

function serveContract(): Promise<Server> {
    const server = createServer((request, response) => {
        if (request.url === "/suwageval/contract") {
            response.setHeader("content-type", "application/json");
            response.end(
                JSON.stringify({ label: "e2e-paging", agents: [{ name: "main", model: { name: "some-model" }, config: "Be helpful." }] }),
            );
        } else {
            response.statusCode = 500;
            response.end();
        }
    });
    return new Promise((resolve) => server.listen(0, "localhost", () => resolve(server)));
}

test("the runs of an agent come a page at a time", async ({ page, request }) => {
    const agentServer = await serveContract();
    try {
        const name = `e2e-paging-${Date.now()}`;
        const endpointUrl = `http://localhost:${(agentServer.address() as AddressInfo).port}`;
        expect((await request.post("/api/agents", { data: { name, endpointUrl } })).ok()).toBe(true);
        const scenario = { key: "greets", samples: 1, conversation: [{ kind: "UserMessage", text: "Hello" }] };
        expect((await request.put(`/api/agents/${name}/scenarios`, { data: { scenarios: [scenario] } })).ok()).toBe(true);
        let newest = "";
        for (let index = 0; index < PAGE_SIZE + EXTRA_RUNS; index++) {
            const created = await request.post("/api/runs", { data: { agent: name } });
            expect(created.ok()).toBe(true);
            newest = ((await created.json()) as { runId: string }).runId.slice(0, 8);
        }

        await page.goto(`/agents/${name}`);
        const runsTable = page.getByRole("table").first();
        const rows = runsTable.getByRole("row");
        await expect(rows).toHaveCount(1 + PAGE_SIZE);
        const loadMore = page.getByRole("button", { name: "Load more", exact: true });
        await expect(loadMore).toBeVisible();

        await page.getByRole("checkbox", { name: `Select run ${newest}`, exact: true }).check();
        await loadMore.click();

        await expect(rows).toHaveCount(1 + PAGE_SIZE + EXTRA_RUNS);
        await expect(loadMore).toHaveCount(0);
        await expect(page.getByRole("checkbox", { name: `Select run ${newest}`, exact: true })).toBeChecked();
    } finally {
        agentServer.close();
    }
});
