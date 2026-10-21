import { describe, expect, it } from "vitest";

import type { CheckRunResult, ConversationItem, RunSummary, ScenarioRunResult } from "./api";
import {
    checkAnchor,
    checkSummary,
    durationSummary,
    formatSeconds,
    failedEvidenceKeys,
    isActive,
    needsFullResults,
    itemBody,
    itemKey,
    itemTitle,
    notJudgedCount,
    percent,
    percentValue,
    progress,
    runHref,
    sampleVerdict,
    samplesToOpen,
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
        judgedByModel: false,
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
            definitionYaml: "",
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
        it("writes seconds with one decimal", () => {
            expect(formatSeconds(9800)).toBe("9.8 s");
            expect(formatSeconds(109_050)).toBe("109.1 s");
        });

        it("summarizes the server's median and p95", () => {
            expect(durationSummary({ medianMs: 3000, p95Ms: 20_000 })).toBe("Turn time: median 3.0 s · p95 20.0 s");
        });

        it("says nothing when no sample made a call", () => {
            expect(durationSummary({ medianMs: null, p95Ms: null })).toBeNull();
            expect(durationSummary({})).toBeNull();
        });
    });
});

describe("checkAnchor", () => {
    it("names a check's row by its scenario and key", () => {
        expect(checkAnchor("refund", "confirms-refund")).toBe("check-refund--confirms-refund");
    });
});

describe("samplesToOpen", () => {
    const sample = (n: number, passed: boolean) => ({ sample: n, passed, message: "", notJudged: false, evidence: [] });
    it("opens the samples the check failed in, else the first", () => {
        expect(samplesToOpen({ samples: [sample(1, true), sample(2, false), sample(3, false)] })).toEqual(["2", "3"]);
        expect(samplesToOpen({ samples: [sample(1, true), sample(2, true)] })).toEqual(["1"]);
        expect(samplesToOpen({ samples: [] })).toEqual([]);
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

describe("needsFullResults", () => {
    const shown = { status: "Running", samplesDone: 3, samplesTotal: 10, cancelRequested: false } as const;

    it("is false while nothing moved", () => {
        expect(needsFullResults(shown, { ...shown })).toBe(false);
    });

    it("is true when a sample was stored", () => {
        expect(needsFullResults(shown, { ...shown, samplesDone: 4 })).toBe(true);
    });

    it("is true when the total became known", () => {
        expect(needsFullResults({ ...shown, samplesTotal: 0 }, shown)).toBe(true);
    });

    it("is true when the status changed, the final one included", () => {
        expect(needsFullResults({ ...shown, status: "Queued" }, shown)).toBe(true);
        expect(needsFullResults(shown, { ...shown, status: "Done" })).toBe(true);
        expect(needsFullResults(shown, { ...shown, status: "Cancelled" })).toBe(true);
    });

    it("is true when a stop was asked for", () => {
        expect(needsFullResults(shown, { ...shown, cancelRequested: true })).toBe(true);
    });
});

describe("sampleVerdict", () => {
    const checkSample = (sample: number, passed: boolean, notJudged = false) => ({ sample, passed, notJudged, message: "", evidence: [] });
    const scenario = (checks: { samples: ReturnType<typeof checkSample>[] }[]): ScenarioRunResult =>
        ({
            key: "refund",
            samples: [1, 2, 3].map((sample) => ({ sample, status: "Done" })),
            checks,
        }) as unknown as ScenarioRunResult;

    it("fails a sample in which any check failed, and counts those checks", () => {
        const refund = scenario([
            { samples: [checkSample(1, true), checkSample(2, false), checkSample(3, true)] },
            { samples: [checkSample(1, true), checkSample(2, false), checkSample(3, false, true)] },
        ]);
        expect(sampleVerdict(refund, 1)).toEqual({ kind: "passed", failedChecks: 0 });
        expect(sampleVerdict(refund, 2)).toEqual({ kind: "failed", failedChecks: 2 });
        expect(sampleVerdict(refund, 3)).toEqual({ kind: "not judged", failedChecks: 0 });
    });

    it("says nothing of a sample no check looked at", () => {
        expect(sampleVerdict(scenario([]), 1).kind).toBe("unchecked");
    });
});
