import { describe, expect, it } from "vitest";

import { tagOptions } from "./tagOptions";

describe("tagOptions", () => {
    it("offers every tag once and the picked ones that are gone, sorted", () => {
        expect(tagOptions(["smoke", "billing", "smoke"], ["gone"])).toEqual(["billing", "gone", "smoke"]);
    });
});
