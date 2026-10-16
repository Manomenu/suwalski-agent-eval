import { describe, expect, it } from "vitest";

import type { CheckRunResult, RegressionState, RegressionSummary, RunResults } from "./api";
import {
    comparedWithHeading,
    groupChecks,
    historyText,
    knownFailuresText,
    regressionCompareHref,
    stateColor,
    stateLabel,
    statedChecks,
    summaryChips,
} from "./regression";

const summary: RegressionSummary = {
    comparedWith: "master",
    history: 5,
    regressions: 1,
    new: 0,
    alreadyBroken: 2,
    knownFailures: 3,
    fixed: 0,
    unstable: 1,
};

function stated(state: RegressionState): { scenarioKey: string; check: CheckRunResult; state: RegressionState } {
    return { scenarioKey: "s", check: { key: state, regression: { state } } as CheckRunResult, state };
}

describe("labels and colours", () => {
    it("names the states", () => {
        expect(stateLabel("AlreadyBroken")).toBe("Already broken");
        expect(stateLabel("KnownFailure")).toBe("Known failure");
        expect(stateLabel("Regression")).toBe("Regression");
    });
    it("colours the states", () => {
        expect(stateColor("Regression")).toBe("red");
        expect(stateColor("New")).toBe("blue");
        expect(stateColor("AlreadyBroken")).toBe("orange");
        expect(stateColor("KnownFailure")).toBe("gray");
        expect(stateColor("Fixed")).toBe("green");
        expect(stateColor("Unstable")).toBe("yellow");
    });
});

describe("headings", () => {
    it("says what the run is compared with", () => {
        expect(comparedWithHeading(summary)).toBe("Compared with master");
        expect(comparedWithHeading({ ...summary, comparedWith: null })).toBe("Compared with every run of the agent");
    });
    it("says how long the history is", () => {
        expect(historyText(summary)).toBe("last 5 reference runs");
        expect(historyText({ ...summary, comparedWith: null })).toBe("last 5 runs");
    });
});

describe("regressionCompareHref", () => {
    it("links the last passing run with this one", () => {
        expect(regressionCompareHref("b", "a")).toBe("/compare?runs=a,b");
    });
    it("has no link when the check never passed", () => {
        expect(regressionCompareHref("b", null)).toBeNull();
        expect(regressionCompareHref("b", undefined)).toBeNull();
    });
});

describe("grouping", () => {
    it("orders attention states, then fixed and unstable, known apart", () => {
        const groups = groupChecks([
            stated("Unstable"),
            stated("AlreadyBroken"),
            stated("KnownFailure"),
            stated("Fixed"),
            stated("Regression"),
            stated("New"),
        ]);
        expect(groups.attention.map((c) => c.state)).toEqual(["Regression", "New", "AlreadyBroken"]);
        expect(groups.brief.map((c) => c.state)).toEqual(["Fixed", "Unstable"]);
        expect(groups.known.map((c) => c.state)).toEqual(["KnownFailure"]);
    });
    it("skips passed checks and checks without a state", () => {
        const results = {
            scenarios: [
                {
                    key: "s",
                    checks: [{ key: "a", regression: { state: "Passed" } }, { key: "b" }, { key: "c", regression: { state: "New" } }],
                },
            ],
        } as unknown as RunResults;
        expect(statedChecks(results).map((c) => c.check.key)).toEqual(["c"]);
    });
});

describe("summaryChips", () => {
    it("lists only the non-zero counts", () => {
        expect(summaryChips(summary).map((c) => `${c.label} ${c.count}`)).toEqual([
            "Regression 1",
            "Already broken 2",
            "Unstable 1",
            "Known failure 3",
        ]);
    });
});

describe("knownFailuresText", () => {
    it("pluralises", () => {
        expect(knownFailuresText(1)).toBe("1 known failure");
        expect(knownFailuresText(5)).toBe("5 known failures");
    });
});
