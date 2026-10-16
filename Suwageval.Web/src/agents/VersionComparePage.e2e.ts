// Comparing two versions: tick two on the agent page, open the comparison, read the summary and the line diff.
// A version only comes from a run, and a run reads the contract the agent serves, so the test is that agent: a tiny HTTP server.
import { createServer, type Server } from "node:http";
import type { AddressInfo } from "node:net";

import { expect, test, type APIRequestContext } from "@playwright/test";

interface Served {
    label: string;
    model: string;
    config: string;
}

function serveContract(served: Served): Promise<Server> {
    const tool = {
        name: "lookup",
        description: "Finds an order.",
        parameters: { type: "object", properties: { id: { type: "string" }, email: { type: "string" }, date: { type: "string" } } },
    };
    const server = createServer((request, response) => {
        if (request.url === "/suwageval/contract") {
            response.setHeader("content-type", "application/json");
            response.end(
                JSON.stringify({
                    label: served.label,
                    agents: [{ name: "main", model: { name: served.model }, config: served.config, tools: [tool] }],
                }),
            );
        } else {
            // Not a turn the test cares about: the run fails, the version stays.
            response.statusCode = 500;
            response.end();
        }
    });
    return new Promise((resolve) => server.listen(0, "localhost", () => resolve(server)));
}

async function run(request: APIRequestContext, agent: string) {
    expect((await request.post("/api/runs", { data: { agent } })).ok()).toBe(true);
}

test("two ticked versions open a comparison with a summary and a line diff", async ({ page, request }) => {
    const served: Served = { label: "e2e-cmp-old", model: "model-a", config: "Be helpful.\nBe brief.\nAnswer in English.\n" };
    const agentServer = await serveContract(served);
    try {
        const name = `e2e-versions-compare-${Date.now()}`;
        const endpointUrl = `http://localhost:${(agentServer.address() as AddressInfo).port}`;
        expect((await request.post("/api/agents", { data: { name, endpointUrl } })).ok()).toBe(true);
        const scenario = { key: "greets", samples: 1, conversation: [{ kind: "UserMessage", text: "Hello" }] };
        expect((await request.put(`/api/agents/${name}/scenarios`, { data: { scenarios: [scenario] } })).ok()).toBe(true);
        await run(request, name);
        Object.assign(served, { label: "e2e-cmp-new", model: "model-b", config: "Be helpful.\nBe kind.\nAnswer in English.\n" });
        await run(request, name);

        await page.goto(`/agents/${name}`);
        const compare = page.getByRole("button", { name: "Compare versions", exact: true });
        const oldVersion = page.getByRole("checkbox", { name: "Select version e2e-cmp-old", exact: true });
        const newVersion = page.getByRole("checkbox", { name: "Select version e2e-cmp-new", exact: true });
        await expect(oldVersion).toBeVisible();
        await expect(compare).toBeDisabled();
        await newVersion.check();
        await expect(compare).toBeDisabled();
        await oldVersion.check();
        await expect(compare).toBeEnabled();
        await compare.click();

        await expect(page.getByRole("heading", { name: "Compare versions", exact: true })).toBeVisible();
        await expect(page.getByText('Label "e2e-cmp-old" → "e2e-cmp-new"', { exact: true })).toBeVisible();
        await expect(page.getByText("Agent main: model model-a → model-b", { exact: true })).toBeVisible();
        await expect(page.getByText("Agent main: config changed (+1 −1 lines)", { exact: true })).toBeVisible();

        await expect(page.locator(".line-diff-row-removed").getByText("Be brief.", { exact: true })).toBeVisible();
        await expect(page.locator(".line-diff-row-added").getByText("Be kind.", { exact: true })).toBeVisible();

        // The long unchanged tail is folded; opening it shows the lines.
        await expect(page.getByText("description: Finds an order.", { exact: true })).toHaveCount(0);
        await page.getByRole("button", { name: /unchanged lines$/ }).click();
        await expect(page.getByText("description: Finds an order.", { exact: true })).toBeVisible();
    } finally {
        agentServer.close();
    }
});

test("a version the agent does not have is said to be missing", async ({ page, request }) => {
    const name = `e2e-versions-missing-${Date.now()}`;
    expect((await request.post("/api/agents", { data: { name, endpointUrl: "http://localhost:1" } })).ok()).toBe(true);
    const missing = "11111111-2222-4333-8444-555555555555";
    await page.goto(`/agents/${name}/versions/compare?a=${missing}&b=${missing}`);
    await expect(page.getByText(`Agent ${name} has no version ${missing} or ${missing}.`, { exact: true })).toBeVisible();
});
