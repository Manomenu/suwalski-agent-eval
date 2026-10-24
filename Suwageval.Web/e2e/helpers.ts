// Steps the browser tests share: the agent under test (a tiny HTTP server), its scenarios and checks, and runs taken to their end.
import { createServer, type Server } from "node:http";
import type { AddressInfo } from "node:net";

import { expect, type APIRequestContext, type Page } from "@playwright/test";

export interface ServedAgent {
    label: string;
    model?: string;
    config?: string;
    reply?: string;
    /** Every turn answers 500: the run still ends, the version stays. */
    failing?: boolean;
    context?: object;
    tools?: object[];
    /** Awaited before each turn is answered. */
    beforeAnswer?: () => Promise<void>;
}

export interface RunningAgent {
    endpointUrl: string;
    close: () => void;
}

const POLL_INTERVALS_MS = [100, 250, 500];

/** The agent under test; changing the fields of `served` changes what its next run sees. */
export async function serveAgent(served: ServedAgent): Promise<RunningAgent> {
    const server: Server = createServer((request, response) => {
        if (request.url === "/suwageval/contract") {
            response.setHeader("content-type", "application/json");
            response.end(
                JSON.stringify({
                    label: served.label,
                    agents: [
                        {
                            name: "main",
                            model: { name: served.model ?? "e2e-model" },
                            config: served.config ?? "Be helpful.",
                            tools: served.tools,
                        },
                    ],
                    context: served.context,
                }),
            );
            return;
        }
        void (served.beforeAnswer?.() ?? Promise.resolve()).then(() => {
            if (served.failing === true) {
                response.statusCode = 500;
                response.end();
                return;
            }
            response.setHeader("content-type", "application/json");
            response.end(JSON.stringify({ items: [{ kind: "AssistantMessage", text: served.reply ?? "Hello there." }] }));
        });
    });
    await new Promise<void>((resolve) => server.listen(0, "localhost", resolve));
    return {
        endpointUrl: `http://localhost:${String((server.address() as AddressInfo).port)}`,
        close: () => {
            server.close();
        },
    };
}

export interface AgentSetup {
    name: string;
    endpointUrl: string;
    scenarios?: object[];
    checks?: object[];
    regression?: object;
}

const GREETING = [{ kind: "UserMessage", text: "Hello" }];

/** The one scenario most tests need: it says hello, one sample. */
const GREETS_SCENARIO = { key: "greets", samples: 1, conversation: GREETING };

export async function createAgent(request: APIRequestContext, setup: AgentSetup): Promise<void> {
    const { name, endpointUrl, regression } = setup;
    expect((await request.post("/api/agents", { data: { name, endpointUrl, regression } })).ok()).toBe(true);
    const scenarios = setup.scenarios ?? [GREETS_SCENARIO];
    expect((await request.put(`/api/eval/agents/${name}/scenarios`, { data: { scenarios } })).ok()).toBe(true);
    if (setup.checks !== undefined) {
        expect((await request.put(`/api/eval/agents/${name}/checks`, { data: { checks: setup.checks } })).ok()).toBe(true);
    }
}

export async function startRun(request: APIRequestContext, data: object): Promise<string> {
    const created = await request.post("/api/eval/runs", { data });
    expect(created.ok()).toBe(true);
    return ((await created.json()) as { runId: string }).runId;
}

async function waitUntilDone(request: APIRequestContext, runId: string): Promise<void> {
    await expect
        .poll(async () => ((await (await request.get(`/api/eval/runs/${runId}`)).json()) as { status: string }).status, {
            timeout: 30_000,
            intervals: POLL_INTERVALS_MS,
        })
        .toBe("Done");
}

/** Starts a run and waits until it is over; its id. */
export async function runToEnd(request: APIRequestContext, data: object): Promise<string> {
    const runId = await startRun(request, data);
    await waitUntilDone(request, runId);
    return runId;
}

/** Counts the calls of `window.print` in `window.printed`, without opening the dialog. */
export async function stubPrint(page: Page): Promise<void> {
    await page.addInitScript(() => {
        (window as unknown as { printed: number }).printed = 0;
        window.print = () => {
            (window as unknown as { printed: number }).printed++;
        };
    });
}

export const printedCount = (page: Page) => page.evaluate(() => (window as unknown as { printed: number }).printed);

/** What a page shows on paper that must not be there: controls, whatever is marked `no-print`, the app's header and navbar. */
export function visibleOnPaperButNotWanted(page: Page): Promise<string[]> {
    return page.locator("button, input, select, textarea, [role=switch], .no-print, header, nav").evaluateAll((elements) =>
        elements
            .filter((element) => {
                const style = getComputedStyle(element);
                const rect = element.getBoundingClientRect();
                return style.display !== "none" && style.visibility !== "hidden" && rect.width > 0 && rect.height > 0;
            })
            .map((element) => element.outerHTML.slice(0, 100)),
    );
}
