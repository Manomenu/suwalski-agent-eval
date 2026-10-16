import { describe, expect, it } from "vitest";

import type { CheckRunResult, RegressionState, RegressionSummary, RunResults } from "./api";
import {
    compareLink,
    comparedWithHeading,
    groupChecks,
    historyMarks,
    historyTime,
    historyText,
    knownFailuresText,
    regressionCompareHref,
    stateColor,
    stateExplanation,
    stateLabel,
    statedChecks,
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
    agentErrors: 0,
};

function stated(state: RegressionState): { scenarioKey: string; check: CheckRunResult; state: RegressionState } {
    return { scenarioKey: "s", check: { key: state, regression: { state } } as CheckRunResult, state };
}

describe("labels and colours", () => {
    it("names the states", () => {
        expect(stateLabel("AlreadyBroken")).toBe("Already broken");
        expect(stateLabel("KnownFailure")).toBe("Known failure");
        expect(stateLabel("Regression")).toBe("Regression");
        expect(stateLabel("AgentError")).toBe("Agent error");
    });
    it("colours the states", () => {
        expect(stateColor("Regression")).toBe("red");
        expect(stateColor("New")).toBe("blue");
        expect(stateColor("AlreadyBroken")).toBe("orange");
        expect(stateColor("KnownFailure")).toBe("gray");
        expect(stateColor("Fixed")).toBe("green");
        expect(stateColor("Unstable")).toBe("yellow");
        expect(stateColor("AgentError")).toBe("grape");
    });
    it("says what each state means, and which ones fail the CLI's exit code", () => {
        const states: RegressionState[] = [
            "Passed",
            "Regression",
            "New",
            "AlreadyBroken",
            "KnownFailure",
            "Fixed",
            "Unstable",
            "AgentError",
        ];
        const failing = states.filter((state) => stateExplanation(state).includes("Fails the CLI's exit code"));
        expect(failing).toEqual(["Regression", "New", "AgentError"]);
        expect(stateExplanation("AlreadyBroken")).toContain("not caused by this run");
    });
});

describe("headings", () => {
    it("says what the run is compared with", () => {
        expect(comparedWithHeading(summary)).toBe("Compared with master");
        expect(comparedWithHeading({ ...summary, comparedWith: null })).toBe("Compared with every run of the agent");
    });
    it("says how long the history is", () => {
        expect(historyText(summary)).toBe("5 reference runs make a steady pass; the history shows up to 10");
        expect(historyText({ ...summary, comparedWith: null })).toBe("5 runs make a steady pass; the history shows up to 10");
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
            stated("AgentError"),
        ]);
        expect(groups.groups.map((g) => g.state)).toEqual(["Regression", "New", "AgentError", "AlreadyBroken", "Fixed", "Unstable"]);
        expect(groups.known.map((c) => c.state)).toEqual(["KnownFailure"]);
    });
    it("collects the checks of a state in one group and leaves empty states out", () => {
        const groups = groupChecks([stated("New"), stated("Regression"), stated("New")]);
        expect(groups.groups.map((g) => [g.state, g.checks.length])).toEqual([
            ["Regression", 1],
            ["New", 2],
        ]);
        expect(groups.known).toEqual([]);
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

describe("knownFailuresText", () => {
    it("pluralises", () => {
        expect(knownFailuresText(1)).toBe("1 known failure");
        expect(knownFailuresText(5)).toBe("5 known failures");
    });
});

const point = (runId: string, passed: boolean, createdAt = "2026-10-17T00:03:00Z") => ({ runId, passed, createdAt });

describe("compareLink", () => {
    const history = [point("aaaa", true), point("bbbb", false), point("cccc", true), point("dddd", false), point("eeee", true)];

    it("compares a fixed check with the newest run it failed in", () => {
        expect(compareLink("now", { state: "Fixed", history })).toEqual({
            label: "Compare with the last failing run",
            href: "/compare?runs=dddd,now",
        });
    });
    it("compares the other states with the last passing run", () => {
        expect(compareLink("now", { state: "Regression", lastPassedRun: "eeee", history })).toEqual({
            label: "Compare with the last passing run",
            href: "/compare?runs=eeee,now",
        });
    });
    it("has no link for an already broken check, or without a run to compare with", () => {
        expect(compareLink("now", { state: "AlreadyBroken", lastPassedRun: "eeee", history })).toBeNull();
        expect(compareLink("now", { state: "New", history: [] })).toBeNull();
        expect(compareLink("now", { state: "Fixed", history: [point("aaaa", true)] })).toBeNull();
        expect(compareLink("now", undefined)).toBeNull();
    });
});

describe("historyTime", () => {
    it("is day/month and hours:minutes", () => {
        expect(historyTime(new Date(2026, 9, 17, 0, 3).toISOString())).toBe("17/10 00:03");
        expect(historyTime(new Date(2026, 0, 5, 13, 45).toISOString())).toBe("05/01 13:45");
    });
});

describe("historyMarks", () => {
    const at = new Date(2026, 9, 17, 0, 3).toISOString();

    it("lists the history oldest first and this run last", () => {
        const marks = historyMarks({
            passed: false,
            regression: {
                state: "Regression",
                history: [point("725c66c4-0000", true, at), point("11111111-0000", false, at)],
            },
        });
        expect(marks.map((mark) => mark.label)).toEqual(["run 725c66c4: passed", "run 11111111: failed", "this run: failed"]);
        expect(marks[0]).toMatchObject({ tooltip: "17/10 00:03 · run 725c66c4 · passed", href: "/runs/725c66c4-0000", outcome: "passed" });
        expect(marks[2]).toMatchObject({ tooltip: "this run · failed", href: null, outcome: "failed" });
    });
    it("marks this run of an agent error apart from a failure", () => {
        const marks = historyMarks({ passed: false, regression: { state: "AgentError", history: [point("725c66c4-0000", true, at)] } });
        expect(marks[1]).toMatchObject({ outcome: "agentError", label: "this run: agent error", tooltip: "this run · agent error" });
    });
    it("shows nothing for a check without history", () => {
        expect(historyMarks({ passed: false, regression: { state: "New", history: [] } })).toEqual([]);
        expect(historyMarks({ passed: true, regression: { state: "Passed" } })).toEqual([]);
        expect(historyMarks({ passed: true })).toEqual([]);
    });
});
