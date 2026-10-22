import { describe, expect, it } from "vitest";

import { clampPage, DEFAULT_PAGE_SIZE, pageCountOf, pageFromSearch, pageOfItems, searchWithPage } from "./pages";

describe("pageCountOf", () => {
    it("rounds up, and an empty list is one empty page", () => {
        expect(pageCountOf(DEFAULT_PAGE_SIZE)).toBe(1);
        expect(pageCountOf(DEFAULT_PAGE_SIZE + 1)).toBe(2);
        expect(pageCountOf(41, 20)).toBe(3);
        expect(pageCountOf(0)).toBe(1);
    });
});

describe("clampPage", () => {
    it("keeps a page that exists and falls back to the last one", () => {
        expect(clampPage(2, 25, 10)).toBe(2);
        expect(clampPage(9, 25, 10)).toBe(3);
        expect(clampPage(5, 0, 10)).toBe(1);
    });
});

describe("pageOfItems", () => {
    const items = Array.from({ length: 21 }, (_, index) => index);

    it("cuts the pages at the page size and keeps the order", () => {
        expect(pageOfItems(items, 1, 10)).toEqual({ items: items.slice(0, 10), page: 1 });
        expect(pageOfItems(items, 3, 10)).toEqual({ items: [20], page: 3 });
        expect(pageOfItems(items, 2, 5).items).toEqual([5, 6, 7, 8, 9]);
    });

    it("shows the last page when the asked one is gone, and an empty first page for no items", () => {
        expect(pageOfItems(items, 9, 10).page).toBe(3);
        expect(pageOfItems([], 1)).toEqual({ items: [], page: 1 });
    });
});

describe("the page in the address", () => {
    it("reads the page, and takes anything unusable for the first one", () => {
        expect(pageFromSearch("?groupsPage=3", "groupsPage")).toBe(3);
        expect(pageFromSearch("?groupsPage=0", "groupsPage")).toBe(1);
        expect(pageFromSearch("?groupsPage=x", "groupsPage")).toBe(1);
        expect(pageFromSearch("?runsPage=4", "groupsPage")).toBe(1);
        expect(pageFromSearch("", "groupsPage")).toBe(1);
    });

    it("replaces only its own parameter, and the first page leaves none", () => {
        expect(searchWithPage("?runs=all", "groupsPage", 2)).toBe("?runs=all&groupsPage=2");
        expect(searchWithPage("?runsPage=3&groupsPage=2", "groupsPage", 1)).toBe("?runsPage=3");
        expect(searchWithPage("?groupsPage=2", "groupsPage", 1)).toBe("");
    });
});
