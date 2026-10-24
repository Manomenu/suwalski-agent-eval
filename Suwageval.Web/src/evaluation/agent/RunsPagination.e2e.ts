// The runs table pages by number: the server cuts the pages, the page is in the address, and a page past the last shows the last.
import { expect, test } from "@playwright/test";

import { createAgent, serveAgent, startRun } from "../../../e2e/helpers";

test("the runs table pages by number, keeps the page in the address and shows the last page for one past it", async ({ page, request }) => {
    const name = `e2e-runs-paging-${String(Date.now())}`;
    const agentServer = await serveAgent({ label: "e2e-runs-paging" });
    try {
        await createAgent(request, { name, endpointUrl: agentServer.endpointUrl });
        const runIds: string[] = [];
        for (let started = 0; started < 21; started++) runIds.push(await startRun(request, { agent: name }));
        const oldest = runIds[0] ?? "";
        const newest = runIds[20] ?? "";
        const runBox = (runId: string) => page.getByRole("checkbox", { name: `Select run ${runId.slice(0, 8)}`, exact: true });

        await page.goto(`/agents/${name}/evaluation`);
        await expect(runBox(newest)).toBeVisible();
        await expect(runBox(oldest)).toHaveCount(0);

        await page.getByRole("button", { name: "2", exact: true }).click();
        await expect(runBox(oldest)).toBeVisible();
        await expect(runBox(newest)).toHaveCount(0);
        await expect(page).toHaveURL(/runsPage=2/);

        await page.goto(`/agents/${name}/evaluation?runsPage=9`);
        await expect(runBox(oldest)).toBeVisible();
    } finally {
        agentServer.close();
    }
});
