import { describe, expect, it } from "vitest";

import { groupSelection, visibleGroups } from "./groups";

const groups = [{ name: "live" }, { name: "old", archivedAt: "2026-10-01T00:00:00Z" }];

describe("visibleGroups", () => {
    it("hides archived groups unless they are asked for", () => {
        expect(visibleGroups(groups, false).map((group) => group.name)).toEqual(["live"]);
        expect(visibleGroups(groups, true).map((group) => group.name)).toEqual(["live", "old"]);
    });
});

describe("groupSelection", () => {
    it("archives the groups in use and restores the archived ones", () => {
        expect(groupSelection(groups, new Set(["live", "old"]))).toEqual({ toArchive: ["live"], toRestore: ["old"], toDelete: null });
    });

    it("deletes only one selected group, and only an archived one", () => {
        expect(groupSelection(groups, new Set(["old"])).toDelete).toBe("old");
        expect(groupSelection(groups, new Set(["live"])).toDelete).toBeNull();
        expect(
            groupSelection([...groups, { name: "older", archivedAt: "2026-09-01T00:00:00Z" }], new Set(["old", "older"])).toDelete,
        ).toBeNull();
    });
});
