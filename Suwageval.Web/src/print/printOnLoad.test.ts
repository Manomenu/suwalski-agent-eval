import { describe, expect, it } from "vitest";

import { printRequested, searchWithoutPrint } from "./printOnLoad";

describe("printRequested", () => {
    it("is true for print=1 only", () => {
        expect(printRequested("?hide=passed&print=1")).toBe(true);
        expect(printRequested("?print=0")).toBe(false);
        expect(printRequested("")).toBe(false);
    });
});

describe("searchWithoutPrint", () => {
    it("drops the print parameter and keeps the others", () => {
        expect(searchWithoutPrint("?tag=a&print=1&hide=passed")).toBe("?tag=a&hide=passed");
    });
    it("is empty when nothing is left", () => {
        expect(searchWithoutPrint("?print=1")).toBe("");
    });
});
