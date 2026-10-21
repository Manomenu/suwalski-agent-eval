// Run groups on the agent page: the runs table lists runs in no group by default (the filter is in the address), the Groups section
// archives a group, shows it again on request and deletes it with its runs after its name is typed. Layout: the Status cell is never cut.
import { createServer, type Server } from "node:http";
import type { AddressInfo } from "node:net";

import { expect, test, type APIRequestContext } from "@playwright/test";

function serveAgent(): Promise<Server> {
    const server = createServer((request, response) => {
        response.setHeader("content-type", "application/json");
        if (request.url === "/suwageval/contract") {
            response.end(
                JSON.stringify({ label: "e2e-groups", agents: [{ name: "main", model: { name: "some-model" }, config: "Be helpful." }] }),
            );
        } else {
            response.end(JSON.stringify({ items: [{ kind: "AssistantMessage", text: "Hello there." }] }));
        }
    });
    return new Promise((resolve) => server.listen(0, "localhost", () => resolve(server)));
}

async function run(request: APIRequestContext, agent: string, group?: string): Promise<string> {
    const created = await request.post("/api/runs", { data: { agent, group } });
    expect(created.ok()).toBe(true);
    const { runId } = (await created.json()) as { runId: string };
    await expect
        .poll(async () => ((await (await request.get(`/api/runs/${runId}`)).json()) as { status: string }).status, { timeout: 30_000 })
        .toBe("Done");
    return runId;
}

test("the runs table hides group runs by default, and a group is archived, shown again and deleted", async ({ page, request }) => {
    const agentServer = await serveAgent();
    try {
        const suffix = String(Date.now());
        const name = `e2e-groups-${suffix}`;
        const group = `e2e-sweep-${suffix}`;
        const endpointUrl = `http://localhost:${String((agentServer.address() as AddressInfo).port)}`;
        expect((await request.post("/api/agents", { data: { name, endpointUrl } })).ok()).toBe(true);
        const scenario = { key: "greets", samples: 1, conversation: [{ kind: "UserMessage", text: "Hello" }] };
        expect((await request.put(`/api/agents/${name}/scenarios`, { data: { scenarios: [scenario] } })).ok()).toBe(true);
        const plain = await run(request, name);
        const grouped = await run(request, name, group);
        const plainBox = page.getByRole("checkbox", { name: `Select run ${plain.slice(0, 8)}`, exact: true });
        const groupedBox = page.getByRole("checkbox", { name: `Select run ${grouped.slice(0, 8)}`, exact: true });

        // No group is the default: the experiment is out of the list, and the Status cell is whole at a laptop's width.
        await page.setViewportSize({ width: 1280, height: 800 });
        await page.goto(`/agents/${name}`);
        await expect(plainBox).toBeVisible();
        await expect(groupedBox).toHaveCount(0);
        const status = page.getByRole("cell", { name: "done", exact: true });
        await expect(status).toBeVisible();
        expect(await status.evaluate((cell) => cell.scrollWidth <= cell.clientWidth)).toBe(true);

        // Any group, All runs and one group by name; the choice is in the address, so a link keeps it.
        const filter = page.getByRole("combobox", { name: "Group", exact: true });
        await filter.click();
        await page.getByRole("option", { name: "Any group", exact: true }).click();
        await expect(groupedBox).toBeVisible();
        await expect(plainBox).toHaveCount(0);
        await expect(page).toHaveURL(/runs=any/);
        await filter.click();
        await page.getByRole("option", { name: "All runs", exact: true }).click();
        await expect(plainBox).toBeVisible();
        await expect(groupedBox).toBeVisible();
        await page.goto(`/agents/${name}?group=${group}`);
        await expect(groupedBox).toBeVisible();
        await expect(plainBox).toHaveCount(0);

        // Nothing spills sideways at a phone's width.
        await page.setViewportSize({ width: 500, height: 800 });
        expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(
            0,
        );
        await page.setViewportSize({ width: 1280, height: 800 });

        // Archive the group: it leaves the Groups section until "Show archived groups" is on.
        await page.goto(`/agents/${name}?runs=all`);
        const archive = page.getByRole("button", { name: "Archive selected groups", exact: true });
        await expect(archive).toBeDisabled();
        await page.getByRole("checkbox", { name: `Select group ${group}`, exact: true }).check();
        await archive.click();
        await expect(page.getByRole("checkbox", { name: `Select group ${group}`, exact: true })).toHaveCount(0);
        await expect(page.getByText("All groups are archived", { exact: false })).toBeVisible();
        await page.getByRole("switch", { name: "Show archived groups", exact: true }).check();
        await expect(page.getByRole("checkbox", { name: `Select group ${group}`, exact: true })).toBeVisible();

        // Delete it: the confirmation says what goes, and waits for the group's name.
        const open = page.getByRole("button", { name: "Delete group", exact: true });
        await expect(open).toBeDisabled();
        await page.getByRole("checkbox", { name: `Select group ${group}`, exact: true }).check();
        await open.click();
        const dialog = page.getByRole("dialog", { name: "Delete this run group?", exact: true });
        await expect(dialog.getByText("1 run, 1 sample and 0 verdicts will be deleted.", { exact: true })).toBeVisible();
        const confirm = dialog.getByRole("button", { name: "Delete", exact: true });
        await expect(confirm).toBeDisabled();
        await dialog.getByRole("textbox", { name: "Type the group name to confirm", exact: true }).fill(group.slice(0, 8));
        await expect(confirm).toBeDisabled();
        await dialog.getByRole("textbox", { name: "Type the group name to confirm", exact: true }).fill(group);
        await expect(confirm).toBeEnabled();
        await confirm.click();

        await expect(dialog).toHaveCount(0);
        await expect(page.getByRole("checkbox", { name: `Select group ${group}`, exact: true })).toHaveCount(0);
        expect((await request.get(`/api/runs/${grouped}`)).status()).toBe(404);
        expect((await request.get(`/api/runs/${plain}`)).status()).toBe(200);
    } finally {
        agentServer.close();
    }
});
