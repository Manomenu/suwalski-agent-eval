import { describe, expect, it } from "vitest";

import { runsPath } from "./paging";

describe("runsPath", () => {
    it("asks for a page of an agent's runs by number and size", () => {
        expect(runsPath("my agent", 1, 20, [])).toBe("/eval/runs?agent=my+agent&page=1&pageSize=20");
    });

    it("repeats the version parameter", () => {
        expect(runsPath("a", 3, 20, ["v1", "v2"])).toBe("/eval/runs?agent=a&page=3&pageSize=20&version=v1&version=v2");
    });

    it("limits the runs to those in no group or in any group", () => {
        expect(runsPath("a", 1, 20, [], null, "None")).toBe("/eval/runs?agent=a&page=1&pageSize=20&grouping=None");
    });

    it("limits the runs to a group when one is given", () => {
        expect(runsPath("a", 1, 20, [], "sweep-1")).toBe("/eval/runs?agent=a&page=1&pageSize=20&group=sweep-1");
    });
});
