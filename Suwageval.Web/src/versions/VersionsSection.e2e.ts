// Archiving versions: select one, archive it (it disappears, and so do its runs), see it again with the Archived usage chip, restore it.
// Deleting: an archived version goes with its runs, after its id is typed in the confirmation.
// A version only comes from a run, and a run reads the contract the agent serves, so the test is that agent: a tiny HTTP server.
import { expect, test } from "@playwright/test";

import { createAgent, runToEnd, serveAgent } from "../../e2e/helpers";

test("versions are archived, shown again on request, restored and deleted with their runs after the id is typed", async ({
    page,
    request,
}) => {
    const served = { label: "e2e-garbage", config: "Be helpful.", failing: true };
    const agentServer = await serveAgent(served);
    try {
        const name = `e2e-versions-${String(Date.now())}`;
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl });
        await runToEnd(request, { agent: name });
        Object.assign(served, { label: "e2e-keep", config: "Be brief." });
        await runToEnd(request, { agent: name });
        const versions = (await (await request.get(`/api/agents/${name}/versions`)).json()) as { id: string; label: string }[];
        const garbage = versions.find((version) => version.label === "e2e-garbage");
        expect(garbage).toBeDefined();
        const id = garbage?.id ?? "";
        const garbageVersion = page.getByRole("checkbox", { name: "Select version e2e-garbage", exact: true });
        const keptVersion = page.getByRole("checkbox", { name: "Select version e2e-keep", exact: true });
        const runRows = page.getByRole("checkbox", { name: /^Select run / });

        await page.goto(`/agents/${name}`);
        const archive = page.getByRole("button", { name: "Archive selected", exact: true });
        await expect(garbageVersion).toBeVisible();
        await expect(archive).toBeDisabled();

        await garbageVersion.check();
        await expect(archive).toBeEnabled();
        await archive.click();
        await expect(garbageVersion).toHaveCount(0);
        await expect(keptVersion).toBeVisible();

        // The runs of an archived version are hidden on the agent's Evaluation page too, unless the Archived chip is on.
        await page.goto(`/agents/${name}/evaluation`);
        await expect(runRows).toHaveCount(1);
        const chip = page.locator("label", { hasText: /^Archived$/ });
        await chip.hover();
        await expect(page.getByRole("tooltip").getByText("Runs of archived versions: hidden by default", { exact: false })).toBeVisible();
        await chip.click();
        await expect(page.getByRole("checkbox", { name: "Archived", exact: true })).toBeChecked();
        await expect(page).toHaveURL(/archived=1/);
        await expect(runRows).toHaveCount(2);

        await page.goto(`/agents/${name}`);
        await page.locator("label", { hasText: /^Archived$/ }).click();
        await expect(garbageVersion).toBeVisible();
        await expect(page.getByRole("cell").getByText("Archived", { exact: true })).toBeVisible();

        await garbageVersion.check();
        await page.getByRole("button", { name: "Unarchive selected", exact: true }).click();
        await expect(page.getByRole("cell").getByText("Archived", { exact: true })).toHaveCount(0);
        await page.locator("label", { hasText: /^Archived$/ }).click();
        await expect(garbageVersion).toBeVisible();

        await garbageVersion.check();
        await archive.click();
        await expect(garbageVersion).toHaveCount(0);
        await page.locator("label", { hasText: /^Archived$/ }).click();
        const open = page.getByRole("button", { name: "Delete", exact: true });
        await expect(open).toBeDisabled();
        await garbageVersion.check();
        await open.click();

        const dialog = page.getByRole("dialog", { name: "Delete this version?", exact: true });
        await expect(dialog.getByText("1 run, 1 sample and 0 verdicts will be deleted.", { exact: true })).toBeVisible();
        const confirm = dialog.getByRole("button", { name: "Delete", exact: true });
        await expect(confirm).toBeDisabled();
        const input = dialog.getByRole("textbox", { name: "Type the version id to confirm", exact: true });
        await input.fill(id.slice(0, 8));
        await expect(confirm).toBeDisabled();
        await input.fill(id);
        await expect(confirm).toBeEnabled();
        await confirm.click();

        await expect(dialog).toHaveCount(0);
        await expect(garbageVersion).toHaveCount(0);
        await expect(keptVersion).toBeVisible();
        expect((await request.get(`/api/agents/${name}/versions/${id}`)).status()).toBe(404);
    } finally {
        agentServer.close();
    }
});

test("a version whose runs are all in a group is hidden by default and listed, marked, with the Groups usage (kept in the address)", async ({
    page,
    request,
}) => {
    const served = { label: "e2e-main", config: "Be helpful." };
    const agentServer = await serveAgent(served);
    try {
        const suffix = String(Date.now());
        const name = `e2e-version-groups-${suffix}`;
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl });
        await runToEnd(request, { agent: name });
        Object.assign(served, { label: "e2e-sweep", config: "Be brief." });
        await runToEnd(request, { agent: name, group: `e2e-sweep-${suffix}` });
        const mainVersion = page.getByRole("checkbox", { name: "Select version e2e-main", exact: true });
        const sweepVersion = page.getByRole("checkbox", { name: "Select version e2e-sweep", exact: true });

        await page.goto(`/agents/${name}`);
        await expect(mainVersion).toBeVisible();
        await expect(sweepVersion).toHaveCount(0);

        await page.getByText("Groups", { exact: true }).click();
        await expect(page.getByRole("checkbox", { name: "Groups", exact: true })).toBeChecked();
        await expect(sweepVersion).toBeVisible();
        await expect(mainVersion).toBeVisible();
        await expect(page.getByText("Group runs only", { exact: true })).toBeVisible();
        expect(page.url()).toContain("usage=evaluation,groups");

        // The older address parameter still reads.
        await page.goto(`/agents/${name}?versions=any`);
        await expect(sweepVersion).toBeVisible();
        await expect(mainVersion).toHaveCount(0);
    } finally {
        agentServer.close();
    }
});

test("a version linked from the Evaluation page opens the Versions list narrowed to it; removing the chip lists the evaluation versions, Groups adds the group-only one", async ({
    page,
    request,
}) => {
    const served = { label: "e2e-main", config: "Be helpful." };
    const agentServer = await serveAgent(served);
    try {
        const suffix = String(Date.now());
        const name = `e2e-version-usage-${suffix}`;
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl });
        await runToEnd(request, { agent: name });
        Object.assign(served, { label: "e2e-sweep", config: "Be brief." });
        await runToEnd(request, { agent: name, group: `e2e-sweep-${suffix}` });
        const mainVersion = page.getByRole("checkbox", { name: "Select version e2e-main", exact: true });
        const sweepVersion = page.getByRole("checkbox", { name: "Select version e2e-sweep", exact: true });
        const rows = page.getByRole("checkbox", { name: /^Select version / });

        await page.goto(`/agents/${name}/evaluation`);
        await page.getByRole("link", { name: "e2e-main", exact: true }).click();

        await expect(page).toHaveURL(/\/versions\?usage=evaluation&version=[0-9a-f-]{36}$/);
        await expect(page.getByRole("checkbox", { name: "Evaluation", exact: true })).toBeChecked();
        await expect(page.getByText(/^version [0-9a-f]{8}$/)).toBeVisible();
        await expect(rows).toHaveCount(1);
        await expect(mainVersion).toBeVisible();

        await page.getByRole("button", { name: /^Remove version / }).click();
        await expect(page.getByText(/^version [0-9a-f]{8}$/)).toHaveCount(0);
        await expect(mainVersion).toBeVisible();
        await expect(sweepVersion).toHaveCount(0);
        expect(page.url()).not.toContain("version=");

        await page.getByText("Groups", { exact: true }).click();
        await expect(page.getByRole("checkbox", { name: "Groups", exact: true })).toBeChecked();
        await expect(sweepVersion).toBeVisible();
    } finally {
        agentServer.close();
    }
});
