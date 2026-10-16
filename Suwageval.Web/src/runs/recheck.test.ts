import { describe, expect, it } from "vitest";

import type { ChangedCheck } from "./api";
import { changesByScenario, judgedNote, planHeadline, recheckDisabledReason, recheckSource } from "./recheck";

const change = (scenarioKey: string, checkKey: string): ChangedCheck => ({ scenarioKey, checkKey, change: "Changed" });

describe("recheckDisabledReason", () => {
    it("lets only a finished run be re-checked", () => {
        expect(recheckDisabledReason("Done")).toBeNull();
        for (const status of ["Queued", "Running", "Failed", "Cancelled"] as const) {
            expect(recheckDisabledReason(status)).toMatch(/finished run/);
        }
    });
});

describe("changesByScenario", () => {
    it("groups the checks by scenario in the order of the plan", () => {
        const groups = changesByScenario([change("b", "b1"), change("a", "a1"), change("b", "b2")]);
        expect(groups.map((group) => [group.scenarioKey, group.checks.map((check) => check.checkKey)])).toEqual([
            ["b", ["b1", "b2"]],
            ["a", ["a1"]],
        ]);
    });
});

describe("planHeadline and judgedNote", () => {
    it("counts the checks", () => {
        expect(planHeadline({ changedChecks: [change("a", "a1")] })).toMatch(/^1 check is evaluated again/);
        expect(planHeadline({ changedChecks: [change("a", "a1"), change("a", "a2")] })).toMatch(/^2 checks are evaluated again/);
    });

    it("says how many call the judge's model, and nothing when none does", () => {
        expect(judgedNote({ judgedChecks: 0 })).toBeNull();
        expect(judgedNote({ judgedChecks: 1 })).toBe("1 judged by a model: it calls the judge's model.");
        expect(judgedNote({ judgedChecks: "2" })).toBe("2 judged by a model: they call the judge's model.");
    });
});

describe("recheckSource", () => {
    it("is the run a re-check was made from, null for a run that called the agent", () => {
        expect(recheckSource({ recheckOf: "abc" })).toBe("abc");
        expect(recheckSource({ recheckOf: null })).toBeNull();
        expect(recheckSource({})).toBeNull();
    });
});
