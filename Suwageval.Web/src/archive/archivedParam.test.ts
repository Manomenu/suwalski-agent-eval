import { describe, expect, it } from "vitest";

import { archivedFromSearch, searchWithArchived } from "./archivedParam";

describe("archivedFromSearch", () => {
    it("is on only for 1", () => {
        expect(archivedFromSearch("?archived=1", "archived")).toBe(true);
        expect(archivedFromSearch("?archived=true", "archived")).toBe(false);
        expect(archivedFromSearch("?x=1", "archived")).toBe(false);
        expect(archivedFromSearch("", "archived")).toBe(false);
    });

    it("reads its own parameter", () => {
        expect(archivedFromSearch("?archivedGroups=1", "archived")).toBe(false);
        expect(archivedFromSearch("?archivedGroups=1", "archivedGroups")).toBe(true);
    });
});

describe("searchWithArchived", () => {
    it("sets and removes the parameter, keeping the others", () => {
        expect(searchWithArchived("?runsPage=2", "archived", true)).toBe("?runsPage=2&archived=1");
        expect(searchWithArchived("?runsPage=2&archived=1", "archived", false)).toBe("?runsPage=2");
        expect(searchWithArchived("?archived=1", "archived", false)).toBe("");
        expect(searchWithArchived("", "archived", true)).toBe("?archived=1");
    });
});
