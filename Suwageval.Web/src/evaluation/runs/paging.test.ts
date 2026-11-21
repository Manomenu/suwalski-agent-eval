import { describe, expect, it } from "vitest";

import { runsQuery } from "./paging";

describe("runsQuery", () => {
    it("asks for a page of an agent's runs by number and size", () => {
        expect(runsQuery("my agent", 1, 20, [])).toEqual({ agent: "my agent", page: 1, pageSize: 20 });
    });

    it("repeats the version parameter", () => {
        expect(runsQuery("a", 3, 20, ["v1", "v2"])).toEqual({ agent: "a", page: 3, pageSize: 20, version: ["v1", "v2"] });
    });

    it("limits the runs to those in no group or in any group", () => {
        expect(runsQuery("a", 1, 20, [], null, "None")).toEqual({ agent: "a", page: 1, pageSize: 20, grouping: "None" });
        expect(runsQuery("a", 1, 20, [], null, "Any")).toEqual({ agent: "a", page: 1, pageSize: 20, grouping: "Any" });
    });

    it("limits the runs to a group when one is given", () => {
        expect(runsQuery("a", 1, 20, [], "sweep-1")).toEqual({ agent: "a", page: 1, pageSize: 20, group: "sweep-1" });
    });
});
