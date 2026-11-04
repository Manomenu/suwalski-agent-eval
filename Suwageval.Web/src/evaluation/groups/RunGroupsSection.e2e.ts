import { expect, test } from "@playwright/test";

import { createAgent, runToEnd, serveAgent } from "../../../e2e/helpers";

test("the runs table hides group runs by default, and a group is archived, shown again and deleted", async ({ page, request }) => {
    const agentServer = await serveAgent({ label: "e2e-groups" });
    try {
        const suffix = String(Date.now());
        const name = `e2e-groups-${suffix}`;
        // Why: as long as a real sweep's name, because the long Group column is what squeezes the others
        const group = `e2e-sweep-${suffix}-smart-router-salesbot`;
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl });
        const [plain, grouped] = await Promise.all([runToEnd(request, { agent: name }), runToEnd(request, { agent: name, group })]);
        const plainBox = page.getByRole("checkbox", { name: `Select run ${plain.slice(0, 8)}`, exact: true });
        const groupedBox = page.getByRole("checkbox", { name: `Select run ${grouped.slice(0, 8)}`, exact: true });

        await page.setViewportSize({ width: 1280, height: 800 });
        await page.goto(`/agents/${name}/evaluation`);
        await expect(plainBox).toBeVisible();
        await expect(groupedBox).toHaveCount(0);
        const status = page.getByRole("cell", { name: "done", exact: true });
        await expect(status).toBeVisible();
        expect(await status.evaluate((cell) => cell.scrollWidth <= cell.clientWidth)).toBe(true);

        await page.setViewportSize({ width: 500, height: 800 });
        // Why: polled because right after the resize the shell is still moving to its phone layout and a frame may overflow for a moment
        await expect
            .poll(() => page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth))
            .toBeLessThanOrEqual(0);
        await page.setViewportSize({ width: 1280, height: 800 });

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
        const clippedBadges = await page
            .locator("td.no-wrap .mantine-Badge-label")
            .evaluateAll((labels) => labels.filter((label) => label.scrollWidth > label.clientWidth).map((label) => label.textContent));
        expect(clippedBadges).toEqual([]);
        await page.goto(`/agents/${name}/evaluation?group=${group}`);
        await expect(groupedBox).toBeVisible();
        await expect(plainBox).toHaveCount(0);

        await page.goto(`/agents/${name}/evaluation?runs=all`);
        const archive = page.getByRole("button", { name: "Archive selected groups", exact: true });
        await expect(archive).toBeDisabled();
        await page.getByRole("checkbox", { name: `Select group ${group}`, exact: true }).check();
        await archive.click();
        await expect(page.getByRole("checkbox", { name: `Select group ${group}`, exact: true })).toHaveCount(0);
        await expect(page.getByText("All groups are archived", { exact: false })).toBeVisible();
        const chip = page
            .locator(".mantine-Group-root")
            .filter({ has: archive })
            .first()
            .locator("label", { hasText: /^Archived$/ });
        await chip.hover();
        await expect(page.getByRole("tooltip").getByText("Archived groups: hidden from the lists", { exact: false })).toBeVisible();
        await chip.click();
        await expect(page).toHaveURL(/archivedGroups=1/);
        await expect(page.getByRole("checkbox", { name: `Select group ${group}`, exact: true })).toBeVisible();

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
        expect((await request.get(`/api/eval/runs/${grouped}`)).status()).toBe(404);
        expect((await request.get(`/api/eval/runs/${plain}`)).status()).toBe(200);
    } finally {
        agentServer.close();
    }
});
