// A check that passed in the newest reference runs of master and fails now is shown as a regression on the run's page,
// with a link to compare with the last passing run.
import { createServer } from "node:http";
import type { AddressInfo } from "node:net";

import { expect, test, type APIRequestContext } from "@playwright/test";

const REFERENCE_RUNS = 5;

async function runToEnd(request: APIRequestContext, data: object): Promise<string> {
    const created = await request.post("/api/runs", { data });
    expect(created.ok()).toBe(true);
    const { runId } = (await created.json()) as { runId: string };
    await expect
        .poll(async () => ((await (await request.get(`/api/runs/${runId}`)).json()) as { status: string }).status, { timeout: 30_000 })
        .toBe("Done");
    return runId;
}

test("A check that passed in the reference runs and fails now is a regression with a link to the last passing run", async ({
    page,
    request,
}) => {
    let answer = "Hello there.";
    const agentServer = createServer((req, response) => {
        response.setHeader("content-type", "application/json");
        if (req.url === "/suwageval/contract") {
            response.end(
                JSON.stringify({
                    label: "e2e-regression",
                    agents: [{ name: "main", model: { name: "e2e-model" }, config: "Be helpful." }],
                }),
            );
        } else {
            response.end(JSON.stringify({ items: [{ kind: "AssistantMessage", text: answer }] }));
        }
    });
    await new Promise<void>((resolve) => agentServer.listen(0, "localhost", resolve));
    try {
        const name = `e2e-regression-${Date.now()}`;
        const endpointUrl = `http://localhost:${(agentServer.address() as AddressInfo).port}`;
        expect((await request.post("/api/agents", { data: { name, endpointUrl } })).ok()).toBe(true);
        const scenario = { key: "greets", conversation: [{ kind: "UserMessage", text: "Hello" }] };
        expect((await request.put(`/api/agents/${name}/scenarios`, { data: { scenarios: [scenario] } })).ok()).toBe(true);
        const check = { type: "text", key: "says-hello", scenarioKey: "greets", contains: "Hello" };
        expect((await request.put(`/api/agents/${name}/checks`, { data: { checks: [check] } })).ok()).toBe(true);

        const origin = { branch: "master", clean: true, pushed: true, defaultBranch: "master" };
        let lastPassed = "";
        for (let i = 0; i < REFERENCE_RUNS; i++) lastPassed = await runToEnd(request, { agent: name, origin });

        answer = "Goodbye.";
        const failing = await runToEnd(request, { agent: name, origin: { ...origin, clean: false } });

        await page.goto(`/runs/${failing}`);
        await expect(page.getByRole("heading", { name: "Compared with master", exact: true })).toBeVisible();
        await expect(page.getByText("greets / says-hello", { exact: true })).toBeVisible();
        // A state's badge says what the state means.
        await page.getByText("Regression", { exact: true }).last().hover();
        await expect(page.getByText("Fails now after passing in every one of the newest reference runs", { exact: false })).toBeVisible();
        const link = page.getByRole("link", { name: "Compare with the last passing run", exact: true });
        await expect(link).toHaveAttribute("href", `/compare?runs=${lastPassed},${failing}`);

        // The check's history is a strip of marks, oldest first, ending with this run; each earlier mark links to its run.
        const strip = page.getByRole("group", { name: "History of the check", exact: true }).first();
        await expect(strip.getByRole("link", { name: /^run [0-9a-f]{8}: passed$/ })).toHaveCount(REFERENCE_RUNS);
        await expect(strip.getByRole("img", { name: "this run: failed", exact: true })).toBeVisible();
        await strip.getByRole("link", { name: `run ${lastPassed.slice(0, 8)}: passed`, exact: true }).hover();
        await expect(page.getByText("Click a mark to open that run.", { exact: false })).toBeVisible();
        await strip.getByRole("link", { name: `run ${lastPassed.slice(0, 8)}: passed`, exact: true }).click();
        await expect(page).toHaveURL(`/runs/${lastPassed}`);

        await page.goto(`/agents/${name}`);
        await expect(page.getByText("Reference", { exact: true })).toHaveCount(REFERENCE_RUNS);
    } finally {
        agentServer.close();
    }
});
