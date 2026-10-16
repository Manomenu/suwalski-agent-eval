import { describe, expect, it } from "vitest";

import { branchesText, DEFAULT_REGRESSION_HISTORY, DEFAULT_SAMPLES, historyText, samplesText } from "./configuration";

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

describe("samplesText", () => {
    it("shows the set number", () => {
        expect(samplesText(5)).toEqual({ text: "5", isDefault: false });
    });

    it("falls back to the default of 3", () => {
        expect(DEFAULT_SAMPLES).toBe(3);
        expect(samplesText(undefined)).toEqual({ text: "3", isDefault: true });
        expect(samplesText(null).isDefault).toBe(true);
    });
});
