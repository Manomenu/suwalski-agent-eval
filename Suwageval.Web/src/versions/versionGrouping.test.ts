import { describe, expect, it } from "vitest";

import { inVersionGrouping, versionGroupingOf } from "./versionGrouping";

describe("versionGroupingOf", () => {
    it("is no group without a parameter and for anything unknown", () => {
        expect(versionGroupingOf("")).toBe("none");
        expect(versionGroupingOf("?versions=bogus")).toBe("none");
        expect(versionGroupingOf("?runs=any")).toBe("none");
    });

    it("reads any group and all versions", () => {
        expect(versionGroupingOf("?versions=any")).toBe("any");
        expect(versionGroupingOf("?runs=all&versions=all")).toBe("all");
    });
});

describe("inVersionGrouping", () => {
    const ordinary = { groupRunsOnly: false };
    const experiment = { groupRunsOnly: true };

    it("lists ordinary versions for none, group runs only for any, every one for all", () => {
        expect([ordinary, experiment].filter((version) => inVersionGrouping(version, "none"))).toEqual([ordinary]);
        expect([ordinary, experiment].filter((version) => inVersionGrouping(version, "any"))).toEqual([experiment]);
        expect([ordinary, experiment].filter((version) => inVersionGrouping(version, "all"))).toEqual([ordinary, experiment]);
    });
});
