import { describe, expect, it } from "vitest";

import {
    allowedVersionIds,
    consideredVersionIds,
    canCompare,
    dataVersionOptions,
    modelsOf,
    runsToCompare,
    versionOptions,
    visibleRuns,
} from "./view";

const old = { id: "v-old", archivedAt: "2026-10-01T10:00:00Z", modelNames: ["small"] };
const current = { id: "v-new", archivedAt: null, modelNames: ["large"] };

describe("visibleRuns", () => {
    const runs = [{ agentVersionId: "v-old" }, { agentVersionId: "v-new" }, { agentVersionId: "v-unknown" }];

    it("hides the runs of an archived version unless asked, and keeps a run of an unknown version", () => {
        expect(visibleRuns(runs, [old, current], false)).toEqual([runs[1], runs[2]]);
        expect(visibleRuns(runs, [old, current], true)).toEqual(runs);
    });
});

describe("visibleRuns with models", () => {
    const runs = [{ agentVersionId: "v-old" }, { agentVersionId: "v-new" }, { agentVersionId: "v-unknown" }];

    it("keeps the runs of the chosen models; no model chosen means all", () => {
        expect(visibleRuns(runs, [old, current], true, ["small"])).toEqual([runs[0]]);
        expect(visibleRuns(runs, [old, current], true, ["small", "large"])).toEqual([runs[0], runs[1]]);
        expect(visibleRuns(runs, [old, current], true, [])).toEqual(runs);
    });

    it("keeps the runs of a version when any of its agents uses a chosen model", () => {
        const mixed = { id: "v-mixed", archivedAt: null, modelNames: ["large", "small"] };
        const mixedRun = { id: "r-mixed", agentVersionId: "v-mixed" };
        expect(visibleRuns([mixedRun], [mixed], false, ["small"])).toEqual([mixedRun]);
        expect(visibleRuns([mixedRun], [mixed], false, ["tiny"])).toEqual([]);
    });

    it("works together with the archived filter", () => {
        expect(visibleRuns(runs, [old, current], false, ["small"])).toEqual([]);
        expect(visibleRuns(runs, [old, current], false, ["large"])).toEqual([runs[1]]);
    });
});

describe("data versions", () => {
    const runs = [{ agentVersionId: "v-a" }, { agentVersionId: "v-b" }, { agentVersionId: "v-none" }];
    const versions = [
        { id: "v-a", archivedAt: null, modelNames: ["large"], dataVersion: "build-1" },
        { id: "v-b", archivedAt: null, modelNames: ["small"], dataVersion: "build-2" },
        { id: "v-none", archivedAt: null, modelNames: ["large"], dataVersion: null },
    ];

    it("lists each data version once, sorted, leaving out versions without retrieval", () => {
        expect(dataVersionOptions([...versions, { dataVersion: "build-1" }]).map((option) => option.value)).toEqual(["build-1", "build-2"]);
        expect(dataVersionOptions([])).toEqual([]);
        const hash = "93a4af9723bf03c6f0d0d6825d1e7786b5015441813b5273fe381e0d2f7351eb";
        expect(
            dataVersionOptions([
                { dataVersion: hash, dataDescription: "" },
                { dataVersion: hash, dataDescription: "12 knowledge base resources, searched by their RAG descriptions" },
            ]),
        ).toEqual([{ value: hash, label: "93a4af97… · 12 knowledge base resources, searched by…" }]);
    });

    it("keeps the runs of the chosen data versions; none chosen means all", () => {
        expect(visibleRuns(runs, versions, false, [], ["build-2"])).toEqual([runs[1]]);
        expect(visibleRuns(runs, versions, false, [], ["build-1", "build-2"])).toEqual([runs[0], runs[1]]);
        expect(visibleRuns(runs, versions, false, [], [])).toEqual(runs);
    });

    it("combines with the model filter", () => {
        expect(visibleRuns(runs, versions, false, ["large"], ["build-2"])).toEqual([]);
        expect(visibleRuns(runs, versions, false, ["large"], ["build-1"])).toEqual([runs[0]]);
    });
});

describe("modelsOf", () => {
    it("lists each model once, sorted", () => {
        expect(modelsOf([{ modelNames: ["b"] }, { modelNames: ["a", "b"] }, { modelNames: ["b"] }])).toEqual(["a", "b"]);
        expect(modelsOf([])).toEqual([]);
    });
});

describe("comparing", () => {
    it("accepts two to twenty runs", () => {
        expect([0, 1, 2, 20, 21].map(canCompare)).toEqual([false, false, true, true, false]);
    });

    it("puts the oldest selected run first, whatever the order of selecting", () => {
        const newestFirst = [{ id: "c" }, { id: "b" }, { id: "a" }];
        expect(runsToCompare(newestFirst, new Set(["c", "a"]))).toEqual(["a", "c"]);
        expect(runsToCompare(newestFirst, new Set(["gone"]))).toEqual([]);
    });
});

describe("allowedVersionIds", () => {
    const withData = { id: "v-data", archivedAt: null, modelNames: ["large"], dataVersion: "build-7" };
    const versions = [old, current, withData];

    it("is null when the filters let every version through", () => {
        expect(allowedVersionIds(versions, true, [], [])).toBeNull();
        expect(allowedVersionIds([current, withData], false, [], [])).toBeNull();
        expect(allowedVersionIds([], false, [], [])).toBeNull();
    });

    it("lists the versions left after hiding archived ones, models and data versions", () => {
        expect(allowedVersionIds(versions, false, [], [])).toEqual(["v-new", "v-data"]);
        expect(allowedVersionIds(versions, true, ["small"], [])).toEqual(["v-old"]);
        expect(allowedVersionIds(versions, true, ["large"], ["build-7"])).toEqual(["v-data"]);
    });

    it("narrows to the chosen versions, combined with the other filters", () => {
        expect(allowedVersionIds(versions, true, [], [], ["v-data"])).toEqual(["v-data"]);
        expect(allowedVersionIds(versions, true, [], [], ["v-old", "v-new"])).toEqual(["v-old", "v-new"]);
        expect(allowedVersionIds(versions, false, [], [], ["v-old", "v-new"])).toEqual(["v-new"]);
        expect(allowedVersionIds(versions, true, ["small"], [], ["v-new"])).toEqual([]);
        expect(allowedVersionIds(versions, true, [], [], ["v-old", "v-new", "v-data"])).toBeNull();
    });

    it("is empty when no version qualifies", () => {
        expect(allowedVersionIds(versions, true, ["none"], [])).toEqual([]);
        expect(allowedVersionIds([old], false, [], [])).toEqual([]);
    });
});

describe("consideredVersionIds", () => {
    it("always lists the versions the filters show, archived ones too when they are shown", () => {
        expect(consideredVersionIds([old, current], true, [], [])).toEqual(["v-old", "v-new"]);
        expect(consideredVersionIds([old, current], false, [], [])).toEqual(["v-new"]);
        expect(consideredVersionIds([old, current], true, ["large"], [])).toEqual(["v-new"]);
        expect(consideredVersionIds([old, current], true, [], [], ["v-old"])).toEqual(["v-old"]);
    });

    it("is empty when the filters show none", () => {
        expect(consideredVersionIds([old], false, [], [])).toEqual([]);
    });
});

describe("versionOptions", () => {
    it("lists the versions newest first, each by its title", () => {
        const versions = [
            { id: "11111111-aaaa", label: "first", createdAt: "2026-10-01T10:00:00Z" },
            { id: "33333333-cccc", label: null, createdAt: "2026-10-03T10:00:00Z" },
            { id: "22222222-bbbb", label: "second", createdAt: "2026-10-02T10:00:00Z" },
        ];
        expect(versionOptions(versions)).toEqual([
            { value: "33333333-cccc", label: "33333333" },
            { value: "22222222-bbbb", label: "second" },
            { value: "11111111-aaaa", label: "first" },
        ]);
    });
});

describe("visibleRuns by version", () => {
    it("keeps only the runs of the chosen versions, none chosen meaning all", () => {
        const runs = [{ agentVersionId: "v-old" }, { agentVersionId: "v-new" }, { agentVersionId: "v-gone" }];
        expect(visibleRuns(runs, [old, current], true, [], [], ["v-new"])).toEqual([{ agentVersionId: "v-new" }]);
        expect(visibleRuns(runs, [old, current], true, [], [], [])).toEqual(runs);
        expect(visibleRuns(runs, [old, current], false, [], [], ["v-old"])).toEqual([]);
    });
});
