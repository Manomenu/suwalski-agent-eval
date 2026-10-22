import { describe, expect, it } from "vitest";

import { GROUPS_PAGE_SIZE, groupSelection, groupsPageOf, pageOfGroups, searchWithGroupsPage, visibleGroups } from "./groups";

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

describe("pageOfGroups", () => {
    const names = Array.from({ length: GROUPS_PAGE_SIZE * 2 + 1 }, (_, index) => index);

    it("cuts the pages at the page size and keeps the order", () => {
        expect(pageOfGroups(names, 1)).toEqual({ groups: names.slice(0, GROUPS_PAGE_SIZE), page: 1, pageCount: 3 });
        expect(pageOfGroups(names, 3)).toEqual({ groups: [GROUPS_PAGE_SIZE * 2], page: 3, pageCount: 3 });
    });

    it("shows the last page when the asked one is gone, and one empty page for no groups", () => {
        expect(pageOfGroups(names, 9).page).toBe(3);
        expect(pageOfGroups([], 1)).toEqual({ groups: [], page: 1, pageCount: 1 });
    });
});

describe("groups page in the address", () => {
    it("reads the page, and takes anything unusable for the first one", () => {
        expect(groupsPageOf("?groupsPage=3")).toBe(3);
        expect(groupsPageOf("?groupsPage=0")).toBe(1);
        expect(groupsPageOf("?groupsPage=x")).toBe(1);
        expect(groupsPageOf("")).toBe(1);
    });

    it("replaces only its own parameter, and the first page leaves none", () => {
        expect(searchWithGroupsPage("?runs=all", 2)).toBe("?runs=all&groupsPage=2");
        expect(searchWithGroupsPage("?runs=all&groupsPage=2", 1)).toBe("?runs=all");
        expect(searchWithGroupsPage("?groupsPage=2", 1)).toBe("");
    });
});
