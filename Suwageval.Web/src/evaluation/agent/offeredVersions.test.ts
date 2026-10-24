import { describe, expect, it } from "vitest";

import { offeredVersions, versionGroupingFor } from "./offeredVersions";

describe("offeredVersions", () => {
    const ordinary = { id: "a", groupRunsOnly: false, modelNames: ["large"] };
    const experiment = { id: "b", groupRunsOnly: true, modelNames: ["small"], data: [{ version: "d1" }] };
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
