import { describe, expect, it } from "vitest";

import type { CheckRunResult, ScenarioRunResult } from "./api";
import { hiddenCounts, hiddenLine, hidePassedOf, searchWithHidePassed, visibleChecks, visibleScenarios } from "./hidePassed";

// "Passed fully" is the server's (RunReportTests); here it is given, and the tests check what the page does with it.
function check(key: string, passedFully: boolean): CheckRunResult {
    return {
        key,
        revision: 1,
        latestRevision: 1,
        judgedByModel: false,
        definition: { type: "text", key, scenarioKey: "s", contains: "x" },
        passed: passedFully,
        passedFully,
        passRate: passedFully ? 1 : 0,
        passThreshold: 1,
        samples: [{ sample: 1, passed: passedFully, notJudged: false, evidence: [], message: "" }],
    };
}

function scenario(key: string, checks: CheckRunResult[], passedFully = checks.every((c) => c.passedFully)): ScenarioRunResult {
    return { key, tags: [], revision: 1, definitionYaml: "", samples: [{ sample: 1, status: "Done" }], checks, passedFully };
}

describe("the address", () => {
    it("reads and writes hide=passed and keeps the other parameters", () => {
        expect(hidePassedOf("?hide=passed")).toBe(true);
        expect(hidePassedOf("?tag=a")).toBe(false);
        expect(hidePassedOf("?hide=other")).toBe(false);
        expect(searchWithHidePassed("?tag=a", true)).toBe("?tag=a&hide=passed");
        expect(searchWithHidePassed("?tag=a&hide=passed", false)).toBe("?tag=a");
        expect(searchWithHidePassed("?hide=passed", false)).toBe("");
        expect(searchWithHidePassed("?hide=passed", true)).toBe("?hide=passed");
    });
});

describe("what stays", () => {
    it("hides the checks the server says passed fully, keeps the others", () => {
        const mixed = scenario("s", [check("ok", true), check("bad", false), check("maybe", false)]);
        expect(visibleChecks(mixed, true).map((c) => c.key)).toEqual(["bad", "maybe"]);
        expect(visibleChecks(mixed, false)).toHaveLength(3);
    });

    it("keeps a scenario the server does not call passed, even with every check passed (e.g. a sample without an answer)", () => {
        const noAnswer = scenario("no-answer", [check("a", true)], false);
        expect(visibleScenarios([noAnswer], true).map((s) => s.key)).toEqual(["no-answer"]);
    });

    it("lists the scenarios that stay", () => {
        const all = [scenario("good", [check("a", true)]), scenario("bad", [check("b", false)])];
        expect(visibleScenarios(all, true).map((s) => s.key)).toEqual(["bad"]);
        expect(visibleScenarios(all, false)).toHaveLength(2);
    });
});

describe("hiddenCounts / hiddenLine", () => {
    it("counts the passed checks and scenarios and says the totals stay", () => {
        const all = [scenario("good", [check("a", true), check("b", true)]), scenario("mixed", [check("c", true), check("d", false)])];
        expect(hiddenCounts(all)).toEqual({ checks: 3, scenarios: 1 });
        expect(hiddenLine({ checks: 3, scenarios: 1 })).toBe(
            "Hidden: 3 passed checks (1 scenario passed entirely and is hidden whole). Samples and Checks at the top still count the whole run.",
        );
        expect(hiddenLine({ checks: 1, scenarios: 0 })).toBe(
            "Hidden: 1 passed check. Samples and Checks at the top still count the whole run.",
        );
    });
});
