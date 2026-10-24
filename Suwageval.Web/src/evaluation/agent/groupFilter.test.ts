import { describe, expect, it } from "vitest";

import { filterOfValue, filterOptions, filterValue, groupFilterOf, groupQuery, searchWithGroupFilter } from "./groupFilter";

describe("groupFilterOf", () => {
    it("is no group without a parameter and for anything unknown", () => {
        expect(groupFilterOf("")).toEqual({ kind: "none" });
        expect(groupFilterOf("?runs=bogus")).toEqual({ kind: "none" });
    });

    it("reads any group, all runs and a group by name, the name first", () => {
        expect(groupFilterOf("?runs=any")).toEqual({ kind: "any" });
        expect(groupFilterOf("?runs=all")).toEqual({ kind: "all" });
        expect(groupFilterOf("?group=sweep-1&runs=all")).toEqual({ kind: "group", name: "sweep-1" });
    });
});

describe("searchWithGroupFilter", () => {
    it("replaces its own parameters and keeps the others; the default leaves none", () => {
        expect(searchWithGroupFilter("?tag=a&group=x", { kind: "any" })).toBe("?tag=a&runs=any");
        expect(searchWithGroupFilter("?runs=all", { kind: "group", name: "sweep-1" })).toBe("?group=sweep-1");
        expect(searchWithGroupFilter("?runs=all", { kind: "none" })).toBe("");
    });
});

describe("groupQuery", () => {
    it("asks for no group, any group or a name, and nothing for all runs", () => {
        expect(groupQuery({ kind: "none" })).toEqual({ group: null, grouping: "None" });
        expect(groupQuery({ kind: "any" })).toEqual({ group: null, grouping: "Any" });
        expect(groupQuery({ kind: "all" })).toEqual({ group: null, grouping: null });
        expect(groupQuery({ kind: "group", name: "sweep-1" })).toEqual({ group: "sweep-1", grouping: null });
    });
});

describe("the select", () => {
    it("round-trips every filter through its option value, a group named like a filter included", () => {
        for (const filter of [{ kind: "none" }, { kind: "any" }, { kind: "all" }, { kind: "group", name: "any" }] as const) {
            expect(filterOfValue(filterValue(filter))).toEqual(filter);
        }
    });

    it("offers the three choices, then the groups in use, and keeps a selected archived group", () => {
        const groups = [{ name: "one" }, { name: "old", archivedAt: "2026-10-01T00:00:00Z" }];

        expect(filterOptions(groups, { kind: "none" }).map((option) => option.label)).toEqual(["No group", "Any group", "All runs", "one"]);
        expect(filterOptions(groups, { kind: "group", name: "old" }).map((option) => option.label)).toEqual([
            "No group",
            "Any group",
            "All runs",
            "one",
            "old",
        ]);
    });
});
