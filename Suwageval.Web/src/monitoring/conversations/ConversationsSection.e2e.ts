// The Monitoring page lists the conversations a deployed agent sent, and one opens with its turns.
import { expect, test, type APIRequestContext } from "@playwright/test";

const CONTRACT = { agents: [{ name: "support", model: { name: "test-model" }, config: "Be helpful." }] };

/** Sends turns as a deployed agent does; the hash it must name comes from the server's answer to a wrong one. */
async function sendTurns(request: APIRequestContext, agent: string, turns: object[], environment = "production"): Promise<void> {
    const batch = { agent, environment, contractHash: "unknown", contract: CONTRACT, turns };
    const refused = await request.post("/api/monitoring/turns", { data: batch });
    expect(refused.status()).toBe(422);
    const hash = /hashes to ([0-9a-f]{64})/.exec(JSON.stringify(await refused.json()))?.[1];
    expect(hash).toBeDefined();
    expect((await request.post("/api/monitoring/turns", { data: { ...batch, contractHash: hash } })).ok()).toBe(true);
}

test("a conversation the agent sent is listed on its Monitoring page and opens with its turns", async ({ page, request }) => {
    const name = `e2e-monitoring-${String(Date.now())}`;
    const data = { name, endpointUrl: "http://localhost:9/", features: { monitoring: {} } };
    expect((await request.post("/api/agents", { data })).ok()).toBe(true);
    await sendTurns(request, name, [
        {
            id: crypto.randomUUID(),
            conversationId: "chat-81",
            at: "2026-10-24T12:00:00Z",
            items: [
                { kind: "UserMessage", text: "Where is order A-17?" },
                { kind: "AssistantMessage", text: "It is on its way." },
            ],
        },
    ]);

    await page.goto(`/agents/${name}/monitoring`);
    await page.getByRole("link", { name: "chat-81", exact: true }).click();

    await expect(page.getByRole("heading", { name: "Conversation chat-81", exact: true })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Turn 1", exact: true })).toBeVisible();
    await expect(page.getByText("Where is order A-17?", { exact: true })).toBeVisible();
    await expect(page.getByText("It is on its way.", { exact: true })).toBeVisible();
    await expect(page.getByRole("navigation").getByRole("link", { name: "Monitoring", exact: true })).toHaveAttribute(
        "data-active",
        "true",
    );
});

test("Versions in monitoring lists the version answering now, marked Current and Not evaluated", async ({ page, request }) => {
    const name = `e2e-monitoring-versions-${String(Date.now())}`;
    const data = { name, endpointUrl: "http://localhost:9/", features: { monitoring: {} } };
    expect((await request.post("/api/agents", { data })).ok()).toBe(true);
    await sendTurns(request, name, [
        {
            id: crypto.randomUUID(),
            conversationId: "chat-1",
            at: new Date().toISOString(),
            items: [
                { kind: "UserMessage", text: "Hi" },
                { kind: "AssistantMessage", text: "Hello" },
            ],
        },
    ]);

    await page.goto(`/agents/${name}/monitoring`);
    await page.getByRole("link", { name: "Versions in monitoring", exact: true }).click();

    await expect(page).toHaveURL(/\/versions\?usage=monitoring$/);
    await expect(page.getByText("Current", { exact: true })).toBeVisible();
    await expect(page.getByText("Not evaluated", { exact: true })).toBeVisible();
    await expect(page.getByRole("columnheader", { name: /Conversations, 30 d/ })).toBeVisible();
});

test("a conversation drafts as a scenario, whole, up to a chosen message, or with the rest simulated", async ({ page, request }) => {
    const name = `e2e-monitoring-draft-${String(Date.now())}`;
    const data = { name, endpointUrl: "http://localhost:9/", features: { monitoring: {} } };
    expect((await request.post("/api/agents", { data })).ok()).toBe(true);
    const turn = (text: string, at: string) => ({
        id: crypto.randomUUID(),
        conversationId: "chat-7",
        at,
        items: [
            { kind: "UserMessage", text },
            { kind: "AssistantMessage", text: `About ${text}` },
        ],
    });
    await sendTurns(request, name, [turn("Where is A-17?", "2026-10-24T12:00:00Z"), turn("And B-2?", "2026-10-24T12:01:00Z")]);

    await page.goto(`/agents/${name}/monitoring`);
    await page.getByRole("link", { name: "chat-7", exact: true }).click();
    await page.getByRole("button", { name: "Scenario YAML", exact: true }).click();
    const yaml = page.getByLabel("Scenario YAML");
    await expect(yaml).toContainText("- assistant: About And B-2?");

    await page.getByRole("combobox", { name: "Copy", exact: true }).click();
    await page.getByRole("option", { name: "Up to a user message", exact: true }).click();
    await page.getByRole("combobox", { name: "Last user message", exact: true }).click();
    await page.getByRole("option", { name: "1. Where is A-17?", exact: true }).click();

    await expect(yaml).toContainText("- user: Where is A-17?");
    await expect(yaml).not.toContainText("About Where is A-17?");
    await expect(yaml).not.toContainText("And B-2?");

    // The rest simulated: as long as the real conversation went on after the cut (the answer to message 1, then message 2), on a click.
    await page.locator("label", { hasText: /^Simulate the rest$/ }).click();
    await expect(page.getByRole("textbox", { name: "Turns", exact: true })).toHaveValue("2");
    await expect(yaml).toHaveCount(0);
    await page.getByRole("button", { name: "Generate scenario", exact: true }).click();
    await expect(yaml).toContainText("turns: 2");
    await expect(yaml).toContainText("simulatedUser:");
    await expect(yaml).not.toContainText("About Where is A-17?");
});

test("the environment filter above the page narrows the conversations, all of them by default", async ({ page, request }) => {
    const name = `e2e-monitoring-environments-${String(Date.now())}`;
    const data = { name, endpointUrl: "http://localhost:9/", features: { monitoring: {} } };
    expect((await request.post("/api/agents", { data })).ok()).toBe(true);
    const turn = (conversationId: string) => ({
        id: crypto.randomUUID(),
        conversationId,
        at: new Date().toISOString(),
        items: [
            { kind: "UserMessage", text: "Hi" },
            { kind: "AssistantMessage", text: "Hello" },
        ],
    });
    await sendTurns(request, name, [turn("in-production")]);
    await sendTurns(request, name, [turn("in-staging")], "staging");

    await page.goto(`/agents/${name}/monitoring`);
    await expect(page.getByRole("link", { name: "in-production", exact: true })).toBeVisible();
    await expect(page.getByRole("link", { name: "in-staging", exact: true })).toBeVisible();

    await page.getByRole("combobox", { name: "Environment", exact: true }).click();
    await page.getByRole("option", { name: "staging", exact: true }).click();

    await expect(page.getByRole("link", { name: "in-staging", exact: true })).toBeVisible();
    await expect(page.getByRole("link", { name: "in-production", exact: true })).toHaveCount(0);
    expect(page.url()).toContain("environment=staging");
});
