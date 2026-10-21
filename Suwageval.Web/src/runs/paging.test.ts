import { describe, expect, it } from "vitest";

import { PAGE_SIZE, runsPath } from "./paging";

describe("runsPath", () => {
    it("asks for the first page of an agent's runs with the page size", () => {
        expect(runsPath("my agent", null, [])).toBe(`/runs?agent=my+agent&limit=${PAGE_SIZE}`);
    });

    it("continues after a run and repeats the version parameter", () => {
        const path = runsPath("a", "run-1", ["v1", "v2"]);
        expect(path).toBe(`/runs?agent=a&limit=${PAGE_SIZE}&before=run-1&version=v1&version=v2`);
    });

    it("limits the runs to a group when one is given", () => {
        expect(runsPath("a", null, [], "sweep-1")).toBe(`/runs?agent=a&limit=${PAGE_SIZE}&group=sweep-1`);
    });
});
