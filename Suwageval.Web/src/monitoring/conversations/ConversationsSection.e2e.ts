// The Monitoring page lists the conversations a deployed agent sent, and one opens with its turns.
import { expect, test, type APIRequestContext } from "@playwright/test";

const CONTRACT = { agents: [{ name: "support", model: { name: "test-model" }, config: "Be helpful." }] };

/** Sends turns as a deployed agent does; the hash it must name comes from the server's answer to a wrong one. */
async function sendTurns(request: APIRequestContext, agent: string, turns: object[]): Promise<void> {
    const batch = { agent, environment: "production", contractHash: "unknown", contract: CONTRACT, turns };
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
