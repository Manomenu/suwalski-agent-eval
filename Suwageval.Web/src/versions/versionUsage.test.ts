import { describe, expect, it } from "vitest";

import { listedVersions, matchesUsage, searchWithFilter, usageOf, usagesOfValues, versionIdsOf } from "./versionUsage";

describe("usageOf", () => {
    it("is evaluation and unused without a parameter and none for an empty one", () => {
        expect(usageOf("")).toEqual(["evaluation", "unused"]);
        expect(usageOf("?tag=a")).toEqual(["evaluation", "unused"]);
        expect(usageOf("?usage=")).toEqual([]);
    });

    it("reads the words in the chips' order and drops the unknown", () => {
        expect(usageOf("?usage=groups,evaluation")).toEqual(["evaluation", "groups"]);
        expect(usageOf("?usage=archived, unused,bogus")).toEqual(["unused", "archived"]);
    });

    it("still reads the older versions parameter", () => {
        expect(usageOf("?versions=any")).toEqual(["groups"]);
        expect(usageOf("?versions=all")).toEqual(["evaluation", "groups", "unused"]);
        expect(usageOf("?versions=bogus")).toEqual(["evaluation", "unused"]);
        expect(usageOf("?versions=any&usage=unused")).toEqual(["unused"]);
    });
});

describe("versionIdsOf", () => {
    it("reads the ids", () => {
        expect(versionIdsOf("")).toEqual([]);
        expect(versionIdsOf("?version=a,b&usage=groups")).toEqual(["a", "b"]);
        expect(versionIdsOf("?version=")).toEqual([]);
    });
});

describe("searchWithFilter", () => {
    it("leaves no parameter for the default and keeps the others", () => {
        expect(searchWithFilter("?usage=groups", ["evaluation", "unused"], [])).toBe("");
        expect(searchWithFilter("?versions=any&versionsPage=2", ["unused", "evaluation"], [])).toBe("?versionsPage=2");
    });

    it("writes the usage in the chips' order, none as an empty parameter, and the ids", () => {
        expect(searchWithFilter("", ["groups", "evaluation"], ["a", "b"])).toBe("?usage=evaluation,groups&version=a,b");
        expect(searchWithFilter("", [], [])).toBe("?usage=");
        expect(searchWithFilter("?version=x", ["evaluation", "unused"], [])).toBe("");
        expect(searchWithFilter("", ["evaluation"], [])).toBe("?usage=evaluation");
    });
});

describe("usagesOfValues", () => {
    it("keeps the known words in order", () => {
        expect(usagesOfValues(["archived", "x", "evaluation"])).toEqual(["evaluation", "archived"]);
    });
});

describe("matchesUsage", () => {
    const ordinary = { runs: 2, groupRuns: 0 };
    const mixed = { runs: 1, groupRuns: 3 };
    const experiment = { runs: 0, groupRuns: 2 };
    const unused = { runs: 0, groupRuns: 0 };
    const archived = { runs: 5, groupRuns: 0, archivedAt: "2026-10-01T00:00:00Z" };
    const all = [ordinary, mixed, experiment, unused, archived];

    it("lists a version that has any of the chosen usages", () => {
        expect(all.filter((v) => matchesUsage(v, ["evaluation"]))).toEqual([ordinary, mixed]);
        expect(all.filter((v) => matchesUsage(v, ["groups"]))).toEqual([mixed, experiment]);
        expect(all.filter((v) => matchesUsage(v, ["unused"]))).toEqual([unused]);
        expect(all.filter((v) => matchesUsage(v, ["groups", "unused"]))).toEqual([mixed, experiment, unused]);
    });

    it("lists an archived version only when archived is chosen", () => {
        expect(matchesUsage(archived, ["evaluation", "groups", "unused"])).toBe(false);
        expect(all.filter((v) => matchesUsage(v, ["archived"]))).toEqual([archived]);
        expect(all.filter((v) => matchesUsage(v, ["evaluation", "archived"]))).toEqual([ordinary, mixed, archived]);
    });

    it("lists nothing for no usage", () => {
        expect(all.filter((v) => matchesUsage(v, []))).toEqual([]);
    });
});

describe("listedVersions", () => {
    it("narrows to the named versions within the usage", () => {
        const versions = [
            { id: "a", runs: 1, groupRuns: 0 },
            { id: "b", runs: 0, groupRuns: 1 },
        ];
        expect(listedVersions(versions, ["evaluation"], []).map((v) => v.id)).toEqual(["a"]);
        expect(listedVersions(versions, ["evaluation", "groups"], ["b"]).map((v) => v.id)).toEqual(["b"]);
        expect(listedVersions(versions, ["evaluation"], ["b"])).toEqual([]);
    });
});
