import { describe, expect, it } from "vitest";

import { archiveSelection, visibleItems } from "./archive";

interface Row {
    name: string;
    archivedAt?: string | null;
}

const live: Row = { name: "live" };
const old: Row = { name: "old", archivedAt: "2026-10-01T00:00:00Z" };
const older: Row = { name: "older", archivedAt: "2026-09-01T00:00:00Z" };
const keyOf = (item: Row) => item.name;

describe("visibleItems", () => {
    it("hides the archived ones unless they are asked for", () => {
        expect(visibleItems([live, old], false)).toEqual([live]);
        expect(visibleItems([live, old], true)).toEqual([live, old]);
    });
});

describe("archiveSelection", () => {
    it("archives the rows in use and restores the archived ones", () => {
        expect(archiveSelection([live, old], new Set(["live", "old"]), keyOf)).toEqual({
            toArchive: ["live"],
            toRestore: ["old"],
            toDelete: null,
        });
    });

    it("selects nothing for nothing or for a key that is not listed", () => {
        expect(archiveSelection([live, old], new Set(), keyOf)).toEqual({ toArchive: [], toRestore: [], toDelete: null });
        expect(archiveSelection([live, old], new Set(["gone"]), keyOf)).toEqual({ toArchive: [], toRestore: [], toDelete: null });
    });

    it("deletes only one selected row, and only an archived one", () => {
        expect(archiveSelection([live, old], new Set(["old"]), keyOf).toDelete).toBe(old);
        expect(archiveSelection([live, old], new Set(["live"]), keyOf).toDelete).toBeNull();
        expect(archiveSelection([live, old, older], new Set(["old", "older"]), keyOf).toDelete).toBeNull();
    });
});
