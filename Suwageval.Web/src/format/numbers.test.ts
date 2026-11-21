import { describe, expect, it } from "vitest";

import { percentValue, seconds } from "./numbers";

describe("percentValue", () => {
    it("rounds to one decimal", () => {
        expect(percentValue(0.12345)).toBe(12.3);
        expect(percentValue(1)).toBe(100);
    });
});

describe("seconds", () => {
    it("rounds milliseconds to seconds with one decimal", () => {
        expect(seconds(1240)).toBe(1.2);
        expect(seconds(1250)).toBe(1.3);
        expect(seconds(0)).toBe(0);
    });
});
