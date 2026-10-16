import { describe, expect, it } from "vitest";

import { ApiError } from "../api/client";
import {
    agentHref,
    canCompare,
    compareHref,
    isNotFound,
    jsonText,
    modelsOf,
    runHref,
    runsToCompare,
    selectionActions,
    shortId,
    versionHref,
    versionLabel,
    versionTitle,
    visibleRuns,
    visibleVersions,
} from "./view";

const old = { id: "v-old", archivedAt: "2026-10-01T10:00:00Z", modelName: "small" };
const current = { id: "v-new", archivedAt: null, modelName: "large" };

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
        expect(runHref("abc")).toBe("/runs/abc");
    });
});

describe("isNotFound", () => {
    it("is true only for a 404 from the server", () => {
        expect(isNotFound(new ApiError("x", 404))).toBe(true);
        expect(isNotFound(new ApiError("x", 500))).toBe(false);
        expect(isNotFound(new Error("x"))).toBe(false);
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

describe("shortId", () => {
    it("keeps eight characters", () => {
        expect(shortId("3f2b8c1e-5d4a")).toBe("3f2b8c1e");
    });
});

describe("versionTitle", () => {
    it("is the label, or the start of the id without one", () => {
        expect(versionTitle("3f2b8c1e-5d4a", "v2")).toBe("v2");
        expect(versionTitle("3f2b8c1e-5d4a", null)).toBe("3f2b8c1e");
    });
});

describe("visibleRuns with models", () => {
    const runs = [{ agentVersionId: "v-old" }, { agentVersionId: "v-new" }, { agentVersionId: "v-unknown" }];

    it("keeps the runs of the chosen models; no model chosen means all", () => {
        expect(visibleRuns(runs, [old, current], true, ["small"])).toEqual([runs[0]]);
        expect(visibleRuns(runs, [old, current], true, ["small", "large"])).toEqual([runs[0], runs[1]]);
        expect(visibleRuns(runs, [old, current], true, [])).toEqual(runs);
    });

    it("works together with the archived filter", () => {
        expect(visibleRuns(runs, [old, current], false, ["small"])).toEqual([]);
        expect(visibleRuns(runs, [old, current], false, ["large"])).toEqual([runs[1]]);
    });
});

describe("modelsOf", () => {
    it("lists each model once, sorted", () => {
        expect(modelsOf([{ modelName: "b" }, { modelName: "a" }, { modelName: "b" }])).toEqual(["a", "b"]);
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
