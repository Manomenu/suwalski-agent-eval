// A check that passed in the newest reference runs of master and fails now is shown as a regression on the run's page,
// with a link to compare with the last passing run. (An agent error is not a regression: regression.test.ts, RegressionStatesTests.)
import { expect, test } from "@playwright/test";

import { createAgent, runToEnd, serveAgent } from "../../../e2e/helpers";

const REFERENCE_RUNS = 2;

test("A check that passed in the reference runs and fails now is a regression with a link to the last passing run", async ({
    page,
    request,
}) => {
    const served = { label: "e2e-regression", reply: "Hello there." };
    const agentServer = await serveAgent(served);
    try {
        const name = `e2e-regression-${String(Date.now())}`;
        const check = { type: "text", key: "says-hello", scenarioKey: "greets", contains: "Hello" };
        await createAgent(request, {
            name,
            endpointUrl: agentServer.endpointUrl,
            checks: [check],
            regression: { history: REFERENCE_RUNS },
        });

        const origin = { branch: "master", clean: true, pushed: true, defaultBranch: "master" };
        let lastPassed = "";
        for (let index = 0; index < REFERENCE_RUNS; index++) lastPassed = await runToEnd(request, { agent: name, origin });

        served.reply = "Goodbye.";
        const failing = await runToEnd(request, { agent: name, origin: { ...origin, clean: false } });

        await page.goto(`/runs/${failing}`);
        await expect(page.getByRole("heading", { name: "Compared with master", exact: true })).toBeVisible();
        await expect(page.getByText("greets / says-hello", { exact: true })).toBeVisible();
        const section = page.getByLabel("Compared with master", { exact: true });
        await expect(section.getByText("(1)", { exact: true })).toBeVisible();
        await section.getByText("Regression", { exact: true }).hover();
        await expect(page.getByText("Fails now after passing in every one of the newest reference runs", { exact: false })).toBeVisible();
        const link = page.getByRole("link", { name: "Compare with the last passing run", exact: true });
        await expect(link).toHaveAttribute("href", `/compare?runs=${lastPassed},${failing}&baseline=${lastPassed}`);

        await expect(page.getByText("Goodbye.", { exact: true })).toBeHidden();
        await section.getByRole("link", { name: "greets / says-hello", exact: true }).click();
        await expect(page).toHaveURL(new RegExp(`/runs/${failing}#check-greets--says-hello$`));
        await expect(page.getByText("Goodbye.", { exact: true })).toBeVisible();

        const strip = page.getByRole("group", { name: "History of the check", exact: true }).first();
        await expect(strip.getByRole("link", { name: /^run [0-9a-f]{8}: passed$/ })).toHaveCount(REFERENCE_RUNS);
        await expect(strip.getByRole("img", { name: "this run: failed", exact: true })).toBeVisible();
        await strip.getByRole("link", { name: `run ${lastPassed.slice(0, 8)}: passed`, exact: true }).click();
        await expect(page).toHaveURL(`/runs/${lastPassed}`);

        await page.goto(`/agents/${name}/evaluation`);
        await expect(page.getByText("Reference", { exact: true })).toHaveCount(REFERENCE_RUNS);
    } finally {
        agentServer.close();
    }
});
