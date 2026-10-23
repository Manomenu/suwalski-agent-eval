import { describe, expect, it } from "vitest";

import {
    inVersionGrouping,
    offeredVersions,
    searchWithVersionGrouping,
    versionGroupingFor,
    versionGroupingOf,
    versionGroupingOfValue,
} from "./versionGrouping";

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

describe("searchWithVersionGrouping", () => {
    it("replaces its own parameter and keeps the others (the runs' too); the default leaves none", () => {
        expect(searchWithVersionGrouping("?runs=any", "all")).toBe("?runs=any&versions=all");
        expect(searchWithVersionGrouping("?versions=all&tag=a", "any")).toBe("?tag=a&versions=any");
        expect(searchWithVersionGrouping("?versions=all", "none")).toBe("");
    });
});

describe("versionGroupingOfValue", () => {
    it("reads the select's value, the default for anything else", () => {
        expect(versionGroupingOfValue("any")).toBe("any");
        expect(versionGroupingOfValue("all")).toBe("all");
        expect(versionGroupingOfValue("none")).toBe("none");
        expect(versionGroupingOfValue(null)).toBe("none");
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

describe("offeredVersions", () => {
    const ordinary = { id: "a", groupRunsOnly: false, modelNames: ["large"] };
    const experiment = { id: "b", groupRunsOnly: true, modelNames: ["small"], dataVersion: "d1" };
    const other = { id: "c", groupRunsOnly: true, modelNames: ["tiny"] };
    const ids = (versions: { id: string }[]) => versions.map((version) => version.id);

    it("offers the versions of the grouping, so a model used only in sweeps is not offered for runs in no group", () => {
        expect(ids(offeredVersions([ordinary, experiment, other], "none", [], [], []))).toEqual(["a"]);
        expect(ids(offeredVersions([ordinary, experiment, other], "any", [], [], []))).toEqual(["b", "c"]);
        expect(ids(offeredVersions([ordinary, experiment, other], "all", [], [], []))).toEqual(["a", "b", "c"]);
    });

    it("keeps what is chosen offered, by version, model or data version", () => {
        expect(ids(offeredVersions([ordinary, experiment, other], "none", [], [], ["c"]))).toEqual(["a", "c"]);
        expect(ids(offeredVersions([ordinary, experiment, other], "none", ["small"], [], []))).toEqual(["a", "b"]);
        expect(ids(offeredVersions([ordinary, experiment, other], "none", [], ["d1"], []))).toEqual(["a", "b"]);
    });
});

describe("versionGroupingFor", () => {
    it("follows the runs table's filter; one group by name shows every version", () => {
        expect(versionGroupingFor({ kind: "none" })).toBe("none");
        expect(versionGroupingFor({ kind: "any" })).toBe("any");
        expect(versionGroupingFor({ kind: "all" })).toBe("all");
        expect(versionGroupingFor({ kind: "group", name: "sweep-1" })).toBe("all");
    });
});
