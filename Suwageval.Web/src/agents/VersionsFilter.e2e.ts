// The Versions filter of the runs table: pick a version and only its runs are listed. A version only comes from a run, and a run reads
// the contract the agent serves, so the test is that agent: a tiny HTTP server whose label the test changes between runs.
import { createServer } from "node:http";
import type { AddressInfo } from "node:net";

import { expect, test } from "@playwright/test";

test("picking a version lists only its runs", async ({ page, request }) => {
    let label = "e2e-flt-old";
    const agentServer = createServer((incoming, response) => {
        if (incoming.url === "/suwageval/contract") {
            response.setHeader("content-type", "application/json");
            response.end(JSON.stringify({ label, agents: [{ name: "main", model: { name: "some-model" }, config: `Be ${label}.` }] }));
        } else {
            // Not a turn the test cares about: the run fails, the version stays.
            response.statusCode = 500;
            response.end();
        }
    });
    await new Promise<void>((resolve) => agentServer.listen(0, "localhost", resolve));
    try {
        const name = `e2e-versions-filter-${Date.now()}`;
        const endpointUrl = `http://localhost:${(agentServer.address() as AddressInfo).port}`;
        expect((await request.post("/api/agents", { data: { name, endpointUrl } })).ok()).toBe(true);
        const scenario = { key: "greets", samples: 1, conversation: [{ kind: "UserMessage", text: "Hello" }] };
        expect((await request.put(`/api/agents/${name}/scenarios`, { data: { scenarios: [scenario] } })).ok()).toBe(true);
        for (const next of ["e2e-flt-old", "e2e-flt-old", "e2e-flt-new"]) {
            label = next;
            expect((await request.post("/api/runs", { data: { agent: name } })).ok()).toBe(true);
        }

        await page.goto(`/agents/${name}`);
        const rows = page.getByRole("table").first().getByRole("row");
        await expect(rows).toHaveCount(1 + 3);

        const filter = page.getByRole("combobox", { name: "Versions", exact: true });
        await filter.click();
        await page.getByRole("option", { name: "e2e-flt-old", exact: true }).click();
        await expect(rows).toHaveCount(1 + 2);

        await page.getByRole("option", { name: "e2e-flt-new", exact: true }).click();
        await expect(rows).toHaveCount(1 + 3);
    } finally {
        agentServer.close();
    }
});
