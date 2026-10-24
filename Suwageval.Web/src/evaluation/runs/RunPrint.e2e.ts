// Printing the run page: the same page on paper (print media, A4 landscape), without controls, with every sample open and the filters
// applied; the Print button; `print=1` in the address opens the print dialog once. The agent is a tiny HTTP server.
import { expect, test } from "@playwright/test";

import {
    createAgent,
    printedCount,
    runToEnd,
    serveAgent,
    stubPrint,
    visibleOnPaperButNotWanted,
    type RunningAgent,
} from "../../../e2e/helpers";
import { PRINT_PAGE_WIDTH } from "../compare/layout";

const CONVERSATION = [{ kind: "UserMessage", text: "Hello" }];
const PAPER = { width: PRINT_PAGE_WIDTH, height: 720 };

let agentServer: RunningAgent;
let name: string;
let runId: string;

test.beforeAll(async ({ request }) => {
    agentServer = await serveAgent({ label: "e2e-print", reply: "Hello there." });
    name = `e2e-print-run-${String(Date.now())}`;
    const scenarios = [
        { key: "greets", tags: ["smoke"], samples: 1, conversation: CONVERSATION },
        { key: "refunds", tags: ["billing"], samples: 1, conversation: CONVERSATION },
    ];
    const checks = [
        { type: "text", key: "says-hello", scenarioKey: "greets", contains: "Hello" },
        { type: "text", key: "mentions-refund", scenarioKey: "greets", contains: "refund" },
        { type: "text", key: "refund-hello", scenarioKey: "refunds", contains: "Hello" },
    ];
    await createAgent(request, { name, endpointUrl: agentServer.endpointUrl, scenarios, checks });
    runId = await runToEnd(request, { agent: name });
});

test.afterAll(() => {
    agentServer.close();
});

test("the Print button is on the screen, calls window.print and is not on paper", async ({ page }) => {
    await stubPrint(page);
    await page.goto(`/runs/${runId}`);
    const button = page.getByRole("button", { name: "Print", exact: true });
    await expect(button).toBeVisible();
    await button.click();
    expect(await printedCount(page)).toBe(1);
    await page.emulateMedia({ media: "print" });
    await expect(button).toBeHidden();
});

test("on paper: no control, header or navbar, every sample open, the line at the top, nothing cut off", async ({ page }) => {
    await page.setViewportSize(PAPER);
    await page.goto(`/runs/${runId}`);
    await expect(page.getByText("says-hello", { exact: true })).toBeVisible();
    // On the screen a sample's conversation is folded.
    await expect(page.getByText("Hello there.", { exact: true }).first()).toBeHidden();

    await page.emulateMedia({ media: "print" });

    await expect(page.getByText("Hello there.", { exact: true }).first()).toBeVisible();
    await expect(page.getByText(`Run ${runId} of ${name} · all scenarios`, { exact: true })).toBeVisible();
    await expect(page.getByRole("heading", { name: `Run ${name}`, exact: true })).toBeVisible();
    await expect(page.getByRole("heading", { name: "greets", exact: true })).toBeVisible();
    await expect(page.getByRole("heading", { name: "refunds", exact: true })).toBeVisible();
    expect(await visibleOnPaperButNotWanted(page)).toEqual([]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)).toBeLessThanOrEqual(0);
});

test("a note is dark text on a light box on paper", async ({ page, request }) => {
    const note = "rebuilt the retrieval index";
    expect((await request.put(`/api/runs/${runId}/note`, { data: { note } })).ok()).toBe(true);
    await page.setViewportSize(PAPER);
    await page.goto(`/runs/${runId}`);
    await page.emulateMedia({ media: "print" });

    for (const text of [page.getByText(note, { exact: true }), page.getByText("Note", { exact: true })]) {
        await expect(text).toBeVisible();
        const channels = await text.evaluate((element) => getComputedStyle(element).color.match(/\d+/g)?.slice(0, 3).map(Number) ?? []);
        expect(channels).toHaveLength(3);
        expect(Math.max(...channels)).toBeLessThan(100);
    }
});

test("the filters stay applied on paper and are named in the line, without the switch and the tag field", async ({ page }) => {
    await page.setViewportSize(PAPER);
    await page.goto(`/runs/${runId}?tag=billing&hide=passed`);
    await expect(page.getByRole("switch", { name: "Hide passed checks", exact: true })).toBeChecked();
    await page.emulateMedia({ media: "print" });

    await expect(
        page.getByText(`Run ${runId} of ${name} · scenarios with tags: billing · passed checks hidden`, { exact: true }),
    ).toBeVisible();
    await expect(page.getByRole("heading", { name: "refunds", exact: true })).toHaveCount(0);
    await expect(page.getByRole("heading", { name: "greets", exact: true })).toHaveCount(0);
    await expect(page.getByRole("switch", { name: "Hide passed checks", exact: true })).toBeHidden();
    await expect(page.getByRole("textbox", { name: "Filter by tag" })).toBeHidden();
    await expect(page.getByText("Hidden:", { exact: false })).toBeHidden();
    expect(await visibleOnPaperButNotWanted(page)).toEqual([]);
});

test("a tag selected on the screen: only its scenarios are printed", async ({ page }) => {
    await page.setViewportSize(PAPER);
    await page.goto(`/runs/${runId}?tag=smoke`);
    await page.emulateMedia({ media: "print" });

    await expect(page.getByText(`Run ${runId} of ${name} · scenarios with tags: smoke`, { exact: true })).toBeVisible();
    await expect(page.getByRole("heading", { name: "greets", exact: true })).toBeVisible();
    await expect(page.getByRole("heading", { name: "refunds", exact: true })).toHaveCount(0);
});

test("print=1 in the address opens the print dialog once, when the page is shown, and leaves the address", async ({ page }) => {
    await stubPrint(page);
    await page.goto(`/runs/${runId}?tag=smoke&print=1`);
    await expect.poll(() => printedCount(page)).toBe(1);
    await expect(page).toHaveURL(new RegExp(`/runs/${runId}\\?tag=smoke$`));
    await page.waitForTimeout(300);
    expect(await printedCount(page)).toBe(1);
});
