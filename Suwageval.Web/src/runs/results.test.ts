import { describe, expect, it } from "vitest";

import { ApiError } from "../api/client";
import type { CheckRunResult, ConversationItem, RunSummary, ScenarioRunResult } from "./api";
import {
    checkSummary,
    failedEvidenceKeys,
    isActive,
    isRunNotFound,
    itemBody,
    itemKey,
    itemTitle,
    notJudgedCount,
    percent,
    progress,
    runHref,
    canCancel,
    isCancelling,
    runStatusLabel,
    statusLabel,
} from "./results";

const call: ConversationItem = { kind: "ToolCall", id: "1", name: "search", arguments: { q: "x" } };
const reply: ConversationItem = { kind: "AssistantMessage", text: "hi" };

function check(samples: CheckRunResult["samples"]): CheckRunResult {
    return {
        key: "c",
        revision: 1,
        latestRevision: 1,
        definition: { type: "text", key: "c", scenarioKey: "s", contains: "x" },
        passed: false,
        passRate: 0.5,
        passThreshold: 1,
        samples,
    };
}

describe("results", () => {
    it("knows which runs are still going", () => {
        expect(isActive("Queued")).toBe(true);
        expect(isActive("Running")).toBe(true);
        expect(isActive("Done")).toBe(false);
        expect(isActive("Failed")).toBe(false);
        expect(isActive("Cancelled")).toBe(false);
    });

    it("offers cancelling only for a queued or running run that nobody asked to stop", () => {
        expect(canCancel({ status: "Queued" })).toBe(true);
        expect(canCancel({ status: "Running", cancelRequested: false })).toBe(true);
        expect(canCancel({ status: "Running", cancelRequested: true })).toBe(false);
        expect(canCancel({ status: "Done" })).toBe(false);
        expect(canCancel({ status: "Failed" })).toBe(false);
        expect(canCancel({ status: "Cancelled", cancelRequested: true })).toBe(false);
    });

    it("says cancelling only while a stopped run is still running", () => {
        expect(isCancelling({ status: "Running", cancelRequested: true })).toBe(true);
        expect(isCancelling({ status: "Running" })).toBe(false);
        expect(isCancelling({ status: "Cancelled", cancelRequested: true })).toBe(false);
        expect(runStatusLabel({ status: "Running", cancelRequested: true })).toBe("cancelling…");
        expect(runStatusLabel({ status: "Running" })).toBe("running");
        expect(runStatusLabel({ status: "Cancelled", cancelRequested: true })).toBe("cancelled");
    });

    it("labels statuses in English", () => {
        expect(statusLabel("Done")).toBe("done");
    });

    it("writes shares as percent without trailing zeros", () => {
        expect(percent(1)).toBe("100%");
        expect(percent(0.5)).toBe("50%");
        expect(percent("0.6667")).toBe("66.7%");
    });

    it("summarizes a check", () => {
        const c = check([
            { sample: 1, passed: true, notJudged: false, evidence: [], message: "" },
            { sample: 2, passed: false, notJudged: false, evidence: [], message: "" },
        ]);
        expect(checkSummary(c)).toBe("50% (1/2 samples, required 100%)");
    });

    it("counts the samples no judge could give a verdict on", () => {
        const c = check([
            { sample: 1, passed: false, notJudged: true, evidence: [], message: "Not judged: the judge model could not be asked." },
            { sample: 2, passed: false, notJudged: false, evidence: [], message: "" },
        ]);
        expect(notJudgedCount(c)).toBe(1);
    });

    it("shows progress, with no total before the run starts", () => {
        const run = { samplesTotal: 0, samplesDone: 0 } as RunSummary;
        expect(progress(run)).toBe("0");
        expect(progress({ ...run, samplesTotal: 4, samplesDone: 3 })).toBe("3/4");
    });

    it("recognizes the server's not-found answer", () => {
        expect(isRunNotFound(new ApiError("There is no run abc.", 404))).toBe(true);
        expect(isRunNotFound(new ApiError("Internal Server Error", 500))).toBe(false);
        expect(isRunNotFound(new Error("There is no run abc."))).toBe(false);
    });

    it("collects the evidence of failed check samples of one sample only", () => {
        const scenario: ScenarioRunResult = {
            key: "s",
            revision: 1,
            samples: [],
            checks: [
                check([
                    { sample: 1, passed: false, notJudged: false, evidence: [call], message: "" },
                    { sample: 2, passed: false, notJudged: false, evidence: [reply], message: "" },
                    { sample: 1, passed: true, notJudged: false, evidence: [reply], message: "" },
                ]),
            ],
        };
        expect(failedEvidenceKeys(scenario, 1)).toEqual(new Set([itemKey(call)]));
    });

    it("titles and bodies items", () => {
        expect(itemTitle(call)).toBe("call: search");
        expect(itemBody(call)).toBe('{\n  "q": "x"\n}');
        expect(itemBody(reply)).toBe("hi");
        expect(itemTitle({ kind: "ToolResult", callId: "1", content: "", isError: true })).toBe("tool result (error)");
    });
});

describe("runHref", () => {
    it("is the run's page", () => {
        expect(runHref("abc")).toBe("/runs/abc");
    });
});
