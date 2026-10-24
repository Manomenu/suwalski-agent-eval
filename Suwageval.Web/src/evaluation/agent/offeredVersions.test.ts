import { describe, expect, it } from "vitest";

import { offeredVersions, versionGroupingFor } from "./offeredVersions";

describe("offeredVersions", () => {
    const ordinary = { id: "a", runs: 2, groupRuns: 0, modelNames: ["large"] };
    const experiment = { id: "b", runs: 0, groupRuns: 1, modelNames: ["small"], data: [{ version: "d1" }] };
    const other = { id: "c", runs: 0, groupRuns: 2, modelNames: ["tiny"] };
    const fresh = { id: "d", runs: 0, groupRuns: 0, modelNames: ["large"] };
    const mixed = { id: "e", runs: 1, groupRuns: 1, modelNames: ["large"] };
    const ids = (versions: { id: string }[]) => versions.map((version) => version.id);

    it("offers the versions of the grouping, so a model used only in sweeps is not offered for runs in no group", () => {
        expect(ids(offeredVersions([ordinary, experiment, other], "none", [], [], []))).toEqual(["a"]);
        expect(ids(offeredVersions([ordinary, experiment, other], "any", [], [], []))).toEqual(["b", "c"]);
        expect(ids(offeredVersions([ordinary, experiment, other], "all", [], [], []))).toEqual(["a", "b", "c"]);
    });

    it("offers a version with no runs yet among the ordinary ones, and one with both kinds of runs in both", () => {
        expect(ids(offeredVersions([fresh, mixed], "none", [], [], []))).toEqual(["d", "e"]);
        expect(ids(offeredVersions([fresh, mixed], "any", [], [], []))).toEqual(["e"]);
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
