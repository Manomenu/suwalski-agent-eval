import { describe, expect, it } from "vitest";

import { branchesText, DEFAULT_REGRESSION_HISTORY, historyText } from "./configuration";

describe("branchesText", () => {
    it("joins the listed branches", () => {
        expect(branchesText(["master", "release"])).toEqual({ text: "master, release", isDefault: false });
    });

    it("says the default branch when none are listed", () => {
        expect(branchesText(undefined)).toEqual({ text: "the repository's default branch", isDefault: true });
        expect(branchesText([]).isDefault).toBe(true);
    });
});

describe("historyText", () => {
    it("shows the set number", () => {
        expect(historyText(3)).toEqual({ text: "3 reference runs", isDefault: false });
        expect(historyText(1).text).toBe("1 reference run");
    });

    it("falls back to the default", () => {
        expect(historyText(null)).toEqual({ text: `${DEFAULT_REGRESSION_HISTORY} reference runs`, isDefault: true });
    });
});
