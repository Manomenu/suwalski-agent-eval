import { describe, expect, it } from "vitest";

import { filteredLabel, keepsTags, selectedTagRows, searchWithTags, selectedTagsOf } from "./tagSelection";

describe("the address", () => {
    it("selects the tags given, each once, and nothing without them", () => {
        expect(selectedTagsOf("?runs=a&tag=procedure&tag=serviceability&tag=procedure&tag=")).toEqual(["procedure", "serviceability"]);
        expect(selectedTagsOf("?runs=a")).toEqual([]);
    });

    it("replaces the tags and keeps every other parameter", () => {
        expect(searchWithTags("?group=sweep&agent=bot&tag=old", ["a b", "c"])).toBe("?group=sweep&agent=bot&tag=a+b&tag=c");
        expect(searchWithTags("?tag=old", [])).toBe("");
    });
});

describe("another parameter", () => {
    it("reads and writes its own tags, leaving the tag filter's alone", () => {
        expect(selectedTagsOf("?tag=a&series=b&series=b", "series")).toEqual(["b"]);
        expect(searchWithTags("?tag=a&series=b", ["c"], "series")).toBe("?tag=a&series=c");
    });
});

describe("narrowing", () => {
    it("keeps a scenario with any selected tag, and every scenario when none is selected", () => {
        expect(keepsTags(["a", "b"], ["b", "c"])).toBe(true);
        expect(keepsTags(["a"], ["b"])).toBe(false);
        expect(keepsTags([], ["b"])).toBe(false);
        expect(keepsTags([], [])).toBe(true);
    });

    it("keeps the rows of the selected tags and drops the row of every check, or keeps all without a filter", () => {
        const rows = [{ tag: null }, { tag: "a" }, { tag: "b" }];
        expect(selectedTagRows(rows, ["b", "c"])).toEqual([{ tag: "b" }]);
        expect(selectedTagRows(rows, [])).toEqual(rows);
    });

    it("names what a narrowed number covers", () => {
        expect(filteredLabel("Checks", [])).toBe("Checks");
        expect(filteredLabel("Checks", ["procedure", "serviceability"])).toBe("Checks (procedure, serviceability)");
    });
});
