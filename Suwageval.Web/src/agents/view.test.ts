import { describe, expect, it } from "vitest";

import { PAGE_SIZE } from "../runs/paging";
import {
    agentHref,
    allowedVersionIds,
    firstPages,
    hasMorePages,
    canCompare,
    compareHref,
    dataText,
    dataVersionsOf,
    jsonText,
    modelsOf,
    runsToCompare,
    selectionActions,
    compareVersionsTitle,
    versionCompareHref,
    versionsToCompare,
    versionHref,
    versionLabel,
    visibleRuns,
    visibleVersions,
} from "./view";

const old = { id: "v-old", archivedAt: "2026-10-01T10:00:00Z", modelNames: ["small"] };
const current = { id: "v-new", archivedAt: null, modelNames: ["large"] };

describe("visibleVersions", () => {
    it("hides the archived ones unless asked", () => {
        expect(visibleVersions([old, current], false)).toEqual([current]);
        expect(visibleVersions([old, current], true)).toEqual([old, current]);
    });
});

describe("visibleRuns", () => {
    const runs = [{ agentVersionId: "v-old" }, { agentVersionId: "v-new" }, { agentVersionId: "v-unknown" }];

    it("hides the runs of an archived version unless asked, and keeps a run of an unknown version", () => {
        expect(visibleRuns(runs, [old, current], false)).toEqual([runs[1], runs[2]]);
        expect(visibleRuns(runs, [old, current], true)).toEqual(runs);
    });
});

describe("selectionActions", () => {
    it("splits the selection into what can be archived and what restored", () => {
        expect(selectionActions([old, current], new Set(["v-old", "v-new"]))).toEqual({ toArchive: ["v-new"], toRestore: ["v-old"] });
        expect(selectionActions([old, current], new Set())).toEqual({ toArchive: [], toRestore: [] });
        expect(selectionActions([old, current], new Set(["gone"]))).toEqual({ toArchive: [], toRestore: [] });
    });
});

describe("links", () => {
    it("encodes the agent name", () => {
        expect(agentHref("my bot")).toBe("/agents/my%20bot");
        expect(versionHref("my bot", "abc")).toBe("/agents/my%20bot/versions/abc");
    });
});

describe("versionLabel", () => {
    it("shows a dash without a label", () => {
        expect(versionLabel({ label: "v2" })).toBe("v2");
        expect(versionLabel({ label: null })).toBe("—");
        expect(versionLabel({ label: "  " })).toBe("—");
    });
});

describe("jsonText", () => {
    it("indents", () => {
        expect(jsonText({ a: 1 })).toBe('{\n  "a": 1\n}');
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
        expect(dataVersionsOf([...versions, { dataVersion: "build-1" }])).toEqual(["build-1", "build-2"]);
        expect(dataVersionsOf([])).toEqual([]);
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

    it("says the data version with its description when there is one", () => {
        expect(dataText({ dataVersion: "build-7", dataDescription: "1832 articles" })).toBe("data build-7 · 1832 articles");
        expect(dataText({ dataVersion: "build-7", dataDescription: " " })).toBe("data build-7");
        expect(dataText({ dataVersion: null })).toBeNull();
    });
});

describe("modelsOf", () => {
    it("lists each model once, sorted", () => {
        expect(modelsOf([{ modelNames: ["b"] }, { modelNames: ["a", "b"] }, { modelNames: ["b"] }])).toEqual(["a", "b"]);
        expect(modelsOf([])).toEqual([]);
    });
});

describe("comparing", () => {
    it("accepts two to five runs", () => {
        expect([0, 1, 2, 5, 6].map(canCompare)).toEqual([false, false, true, true, false]);
    });

    it("links the runs in the order given", () => {
        expect(compareHref(["a", "b"])).toBe("/compare?runs=a,b");
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

    it("is empty when no version qualifies", () => {
        expect(allowedVersionIds(versions, true, ["none"], [])).toEqual([]);
        expect(allowedVersionIds([old], false, [], [])).toEqual([]);
    });
});

describe("firstPages", () => {
    const items = Array.from({ length: PAGE_SIZE * 2 + 1 }, (_, index) => index);

    it("shows a page, then the next one, and everything once the pages cover the list", () => {
        expect(firstPages(items, 1)).toHaveLength(PAGE_SIZE);
        expect(firstPages(items, 2)).toHaveLength(PAGE_SIZE * 2);
        expect(firstPages(items, 3)).toEqual(items);
    });

    it("says there is more until the shown pages cover the list", () => {
        expect(hasMorePages(items, 1)).toBe(true);
        expect(hasMorePages(items, 2)).toBe(true);
        expect(hasMorePages(items, 3)).toBe(false);
        expect(hasMorePages(items.slice(0, PAGE_SIZE), 1)).toBe(false);
        expect(hasMorePages([], 1)).toBe(false);
    });
});

describe("versionsToCompare", () => {
    const versions = [{ id: "a" }, { id: "b" }, { id: "c" }];

    it("gives the two selected versions in the list's order", () => {
        expect(versionsToCompare(versions, new Set(["c", "a"]))).toEqual(["a", "c"]);
    });

    it("gives nothing for fewer or more than two", () => {
        expect(versionsToCompare(versions, new Set())).toBeNull();
        expect(versionsToCompare(versions, new Set(["a"]))).toBeNull();
        expect(versionsToCompare(versions, new Set(["a", "b", "c"]))).toBeNull();
    });

    it("ignores a selected version the list no longer shows", () => {
        expect(versionsToCompare(versions, new Set(["a", "gone"]))).toBeNull();
    });

    it("names the page and says why it cannot open yet", () => {
        expect(versionCompareHref("my bot", "a", "b")).toBe("/agents/my%20bot/versions/compare?a=a&b=b");
        expect(compareVersionsTitle(false)).toContain("exactly two");
        expect(compareVersionsTitle(true)).toContain("changed");
    });
});
