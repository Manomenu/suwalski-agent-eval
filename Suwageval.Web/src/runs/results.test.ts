import { describe, expect, it } from "vitest";

import type { CheckRunResult, ConversationItem, RunSummary, ScenarioRunResult } from "./api";
import {
    checkSummary,
    durationSummary,
    formatSeconds,
    failedEvidenceKeys,
    isActive,
    itemBody,
    itemKey,
    itemTitle,
    median,
    notJudgedCount,
    p95,
    percent,
    percentValue,
    progress,
    runHref,
    seconds,
    shortId,
    versionTitle,
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
        expect(percent(0.6667)).toBe("66.7%");
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
        expect(
            itemBody({
                kind: "Retrieval",
                query: "q",
                chunks: [
                    { text: "a", source: "s1", score: 0.5 },
                    { text: "b", source: "s2" },
                ],
            }),
        ).toBe("[s1, 0.50] a\n\n[s2] b");
        expect(itemTitle({ kind: "ToolResult", callId: "1", content: "", isError: true })).toBe("tool result (error)");
    });

    describe("durations", () => {
        function scenarioWith(...durations: (number | null)[]): ScenarioRunResult {
            return {
                key: "s",
                revision: 1,
                checks: [],
                samples: durations.map((durationMs, index) => ({ sample: index + 1, status: "Done", durationMs })),
            };
        }

        it("writes seconds with one decimal", () => {
            expect(formatSeconds(9800)).toBe("9.8 s");
            expect(formatSeconds(109_050)).toBe("109.1 s");
        });

        it("takes the median, the mean of the middle two for an even count", () => {
            expect(median([5, 1, 3])).toBe(3);
            expect(median([4, 1, 2, 3])).toBe(2.5);
        });

        it("takes p95 by nearest rank", () => {
            const values = Array.from({ length: 20 }, (_, index) => index + 1);
            expect(p95(values)).toBe(19);
            expect(p95([7])).toBe(7);
            expect(p95([3, 1, 2])).toBe(3);
        });

        it("summarizes the samples that made a call, across scenarios", () => {
            expect(durationSummary([scenarioWith(1000, null), scenarioWith(3000, 20_000)])).toBe("Turn time: median 3.0 s · p95 20.0 s");
        });

        it("says nothing when no sample made a call", () => {
            expect(durationSummary([scenarioWith(null)])).toBeNull();
            expect(durationSummary([])).toBeNull();
        });
    });
});

describe("runHref", () => {
    it("is the run's page", () => {
        expect(runHref("abc")).toBe("/runs/abc");
    });
});

describe("shortId", () => {
    it("keeps eight characters", () => {
        expect(shortId("3f2b8c1e-5d4a")).toBe("3f2b8c1e");
    });
});

describe("versionTitle", () => {
    it("is the label, or the start of the id without one", () => {
        expect(versionTitle("3f2b8c1e-5d4a", "v2")).toBe("v2");
        expect(versionTitle("3f2b8c1e-5d4a", null)).toBe("3f2b8c1e");
    });
});

describe("percentValue", () => {
    it("rounds to one decimal", () => {
        expect(percentValue(0.12345)).toBe(12.3);
        expect(percentValue(1)).toBe(100);
    });
});

describe("seconds", () => {
    it("rounds milliseconds to seconds with one decimal", () => {
        expect(seconds(1240)).toBe(1.2);
        expect(seconds(1250)).toBe(1.3);
        expect(seconds(0)).toBe(0);
    });
});
