import { describe, expect, it } from "vitest";

import type { CheckRunResult, SampleResult, ScenarioRunResult } from "./api";
import {
    checkPassed,
    hiddenCounts,
    hiddenLine,
    hidePassedOf,
    scenarioPassed,
    searchWithHidePassed,
    visibleChecks,
    visibleScenarios,
} from "./hidePassed";

function check(key: string, passed: boolean, notJudged = false): CheckRunResult {
    return {
        key,
        revision: 1,
        latestRevision: 1,
        judgedByModel: false,
        definition: { type: "text", key, scenarioKey: "s", contains: "x" },
        passed,
        passRate: passed ? 1 : 0,
        passThreshold: 1,
        samples: [{ sample: 1, passed, notJudged, evidence: [], message: "" }],
    };
}

function scenario(key: string, checks: CheckRunResult[], status: SampleResult["status"] = "Done"): ScenarioRunResult {
    return { key, tags: [], revision: 1, definitionYaml: "", samples: [{ sample: 1, status }], checks };
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
    it("hides a passed check; keeps a failed one and one with a sample not judged", () => {
        expect(checkPassed(check("a", true))).toBe(true);
        expect(checkPassed(check("a", false))).toBe(false);
        expect(checkPassed(check("a", true, true))).toBe(false);
        const mixed = scenario("s", [check("ok", true), check("bad", false), check("maybe", true, true)]);
        expect(visibleChecks(mixed, true).map((c) => c.key)).toEqual(["bad", "maybe"]);
        expect(visibleChecks(mixed, false)).toHaveLength(3);
    });

    it("hides a scenario only when every check passed and every sample answered", () => {
        expect(scenarioPassed(scenario("s", [check("a", true)]))).toBe(true);
        expect(scenarioPassed(scenario("s", [check("a", true), check("b", false)]))).toBe(false);
        expect(scenarioPassed(scenario("s", [check("a", true)], "Failed"))).toBe(false);
        expect(scenarioPassed(scenario("s", [check("a", true)], "NotSimulated"))).toBe(false);
        expect(scenarioPassed(scenario("s", [check("a", true, true)]))).toBe(false);
        expect(scenarioPassed(scenario("s", []))).toBe(false);
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
