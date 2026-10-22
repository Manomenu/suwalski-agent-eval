import { describe, expect, it } from "vitest";

import type { UnstableCheck } from "./api";
import { findUnstable, passedOfRuns, unstableChecksPath, unstableInRuns, unstableRowTooltip, unstableTooltip } from "./stability";

const item = (over: Partial<UnstableCheck> = {}): UnstableCheck => ({
    scenarioKey: "refund",
    checkKey: "says-sorry",
    checkRevision: 1,
    agentVersionId: "v1",
    runs: 9,
    passedRuns: 7,
    newestFailedRunId: "r1",
    ...over,
});

describe("unstableChecksPath", () => {
    it("asks the default versions without a query and for given ones with repeated parameters", () => {
        expect(unstableChecksPath("my agent", [])).toBe("/agents/my%20agent/unstable-checks");
        expect(unstableChecksPath("a", ["v1", "v2"])).toBe("/agents/a/unstable-checks?version=v1&version=v2");
    });
});

describe("findUnstable", () => {
    it("matches version, scenario, check and revision (an edited check is another check)", () => {
        const list = [item()];
        expect(findUnstable(list, "v1", "refund", "says-sorry", 1)).toBe(list[0]);
        expect(findUnstable(list, "v2", "refund", "says-sorry", 1)).toBeUndefined();
        expect(findUnstable(list, "v1", "other", "says-sorry", 1)).toBeUndefined();
        expect(findUnstable(list, "v1", "refund", "other", 1)).toBeUndefined();
        expect(findUnstable(list, "v1", "refund", "says-sorry", 2)).toBeUndefined();
    });
});

describe("tooltips and text", () => {
    it("says how often the check failed among the runs of the version", () => {
        expect(unstableTooltip(item())).toBe("Unstable: failed in 2 of 9 runs of this version");
        expect(passedOfRuns(item())).toBe("7/9");
    });

    it("names the versions when a row is unstable in several", () => {
        const second = item({ agentVersionId: "12345678-aaaa", runs: 3, passedRuns: 2 });
        expect(unstableRowTooltip([item()])).toBe("Unstable: failed in 2 of 9 runs of this version");
        expect(unstableRowTooltip([item({ versionLabel: "v-one" }), second])).toBe(
            "Unstable: failed in 2 of 9 runs of version v-one; failed in 1 of 3 runs of version 12345678",
        );
    });
});

describe("unstableInRuns", () => {
    const cell = (revision: number) => ({ revision });

    it("finds the check once per compared version, by the revision each run used", () => {
        const list = [item(), item({ agentVersionId: "v2", checkRevision: 2 })];
        const found = unstableInRuns(list, ["v1", "v1", "v2", "v3"], [cell(1), cell(1), cell(2), cell(1)], "refund", "says-sorry");
        expect(found.map((x) => x.agentVersionId)).toEqual(["v1", "v2"]);
        expect(unstableInRuns(list, ["v2"], [cell(1)], "refund", "says-sorry")).toEqual([]);
        expect(unstableInRuns(list, ["v1"], [cell(1)], "refund", "missing")).toEqual([]);
        expect(unstableInRuns(list, ["v1", "v2"], [null, cell(2)], "refund", "says-sorry").map((x) => x.agentVersionId)).toEqual(["v2"]);
    });
});
