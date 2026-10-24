// Printing the comparison: the same page on paper (print media, A4 landscape), PRINT_MAX_RUNS run columns inside the paper at their
// printed width, every scenario row open, no control; the Print button is off above the cap. The agent is a tiny HTTP server; the
// runs are PRINT_MAX_RUNS + 1 of one group.
import { expect, test, type Page } from "@playwright/test";

import {
    createAgent,
    printedCount,
    runToEnd,
    serveAgent,
    stubPrint,
    visibleOnPaperButNotWanted,
    type RunningAgent,
} from "../../../e2e/helpers";
import { PRINT_COLUMN_WIDTH, PRINT_LABEL_COLUMN_WIDTH, PRINT_MAX_RUNS, PRINT_PAGE_WIDTH } from "./layout";

const CONVERSATION = [{ kind: "UserMessage", text: "Hello" }];
const PAPER = { width: PRINT_PAGE_WIDTH, height: 720 };

let agentServer: RunningAgent;
let name: string;
let group: string;
let runIds: string[];

test.beforeAll(async ({ request }) => {
    const served = { label: "e2e-print-compare", reply: "Hello there." };
    agentServer = await serveAgent(served);
    const suffix = String(Date.now());
    name = `e2e-print-compare-${suffix}`;
    group = `e2e-print-${suffix}`;
    const scenarios = [
        { key: "greets", tags: ["smoke"], samples: 1, conversation: CONVERSATION },
        { key: "refunds", tags: ["billing"], samples: 1, conversation: CONVERSATION },
    ];
    const checks = [
        { type: "text", key: "says-hello", scenarioKey: "greets", contains: "Hello" },
        { type: "text", key: "mentions-refund", scenarioKey: "greets", contains: "refund" },
        { type: "text", key: "refund-mentioned", scenarioKey: "refunds", contains: "refund" },
    ];
    await createAgent(request, { name, endpointUrl: agentServer.endpointUrl, scenarios, checks });
    // The first run's answer lacks the refund, the others have it: both scenarios differ between the columns.
    const first = await runToEnd(request, { agent: name, group });
    served.reply = "Hello, your refund is on its way.";
    const others = await Promise.all(Array.from({ length: PRINT_MAX_RUNS }, () => runToEnd(request, { agent: name, group })));
    runIds = [first, ...others];
});

test.afterAll(() => {
    agentServer.close();
});

const fitting = (extra = "") => `/compare?${new URLSearchParams({ runs: runIds.slice(0, PRINT_MAX_RUNS).join(",") }).toString()}${extra}`;

async function openForPaper(page: Page, address: string) {
    await page.setViewportSize(PAPER);
    await page.goto(address);
    await expect(page.getByRole("heading", { name: `Compare runs of ${name}`, exact: true })).toBeVisible();
    await page.emulateMedia({ media: "print" });
}

test("the Print button is on the screen, calls window.print and is not on paper", async ({ page }) => {
    await stubPrint(page);
    await page.goto(fitting());
    const button = page.getByRole("button", { name: "Print", exact: true });
    await expect(button).toBeEnabled();
    await button.click();
    expect(await printedCount(page)).toBe(1);
    await page.emulateMedia({ media: "print" });
    await expect(button).toBeHidden();
});

test("above the cap the Print button is off and says why", async ({ page }) => {
    // PRINT_MAX_RUNS + 1 runs picked one by one.
    await page.goto(`/compare?${new URLSearchParams({ runs: runIds.join(",") }).toString()}`);
    await expect(page.getByRole("button", { name: "Print", exact: true })).toBeDisabled();
    await expect(
        page.getByText(
            `At most ${String(PRINT_MAX_RUNS)} runs fit a printed page — compare fewer runs, e.g. with the same baseline, and print each.`,
            { exact: true },
        ),
    ).toBeVisible();
});

test("on paper PRINT_MAX_RUNS columns fit the paper at their printed width, every row is open, no control shows", async ({ page }) => {
    await openForPaper(page, fitting());

    const table = page.getByRole("table").first();
    // One pass in the page's own coordinates: the table's box and every header of its first row.
    const { box, heads, scrolls, pageOverflow } = await table.evaluate((element) => {
        const rect = element.getBoundingClientRect();
        const ths = Array.from(element.querySelectorAll(":scope > thead > tr > th")).map((th) => {
            const r = th.getBoundingClientRect();
            return { left: r.left, right: r.right, width: r.width };
        });
        // Nothing is cut: neither the table's scroll container nor the page scrolls sideways.
        const container = element.closest(".mantine-Table-scrollContainer");
        return {
            box: { x: rect.left, width: rect.width },
            heads: ths,
            scrolls: container === null ? 0 : container.scrollWidth - container.clientWidth,
            pageOverflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
        };
    });
    expect(heads).toHaveLength(PRINT_MAX_RUNS + 1);
    for (const head of heads) {
        expect(head.left).toBeGreaterThanOrEqual(box.x - 1);
        expect(head.right).toBeLessThanOrEqual(box.x + box.width + 1);
        expect(head.right).toBeLessThanOrEqual(PAPER.width);
    }
    expect(scrolls).toBeLessThanOrEqual(0);
    expect(pageOverflow).toBeLessThanOrEqual(0);
    expect(heads[0]?.width).toBeGreaterThanOrEqual(PRINT_LABEL_COLUMN_WIDTH - 1);
    for (const head of heads.slice(1)) expect(head.width).toBeGreaterThanOrEqual(PRINT_COLUMN_WIDTH - 1);
    expect(await table.evaluate((element) => element.scrollWidth - element.clientWidth)).toBeLessThanOrEqual(0);
    expect(await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)).toBeLessThanOrEqual(0);

    await expect(page.getByText("What differs", { exact: true })).toBeVisible();
    for (const key of ["greets", "refunds"]) {
        await expect(page.getByRole("table", { name: `Checks of ${key} by run`, exact: true })).toBeVisible();
    }
    expect(await visibleOnPaperButNotWanted(page)).toEqual([]);
});

test("the line names the runs, the baseline and the filters; they stay applied; the baseline is named without its button", async ({
    page,
}) => {
    const baseline = runIds[0] ?? "";
    await openForPaper(page, fitting(`&baseline=${baseline}&tag=billing`));

    const line = page.locator(".print-line");
    await expect(line).toBeVisible();
    await expect(line).toContainText(`baseline ${baseline.slice(0, 8)}`);
    await expect(line).toContainText("scenarios with tags: billing");
    await expect(page.getByRole("table", { name: "Checks of refunds by run", exact: true })).toBeVisible();
    await expect(page.getByRole("table", { name: "Checks of greets by run", exact: true })).toHaveCount(0);
    await expect(page.getByText("Baseline", { exact: true }).filter({ visible: true })).toHaveCount(1);
    expect(await visibleOnPaperButNotWanted(page)).toEqual([]);
});

test("print=1 in the address opens the print dialog once, when the page is shown, and leaves the address", async ({ page }) => {
    await stubPrint(page);
    await page.goto(fitting("&print=1"));
    await expect.poll(() => printedCount(page)).toBe(1);
    await expect(page).not.toHaveURL(/print=1/);
    await page.waitForTimeout(300);
    expect(await printedCount(page)).toBe(1);
});
