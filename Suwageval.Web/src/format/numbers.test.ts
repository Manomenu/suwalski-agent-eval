import { describe, expect, it } from "vitest";

import { formatSeconds, percent, percentValue, seconds } from "./numbers";

describe("percentValue", () => {
    it("rounds to one decimal", () => {
        expect(percentValue(0.12345)).toBe(12.3);
        expect(percentValue(1)).toBe(100);
    });
});

describe("percent", () => {
    it("writes shares as percent without trailing zeros", () => {
        expect(percent(1)).toBe("100%");
        expect(percent(0.5)).toBe("50%");
        expect(percent(0.6667)).toBe("66.7%");
    });
});

describe("seconds", () => {
    it("rounds milliseconds to seconds with one decimal", () => {
        expect(seconds(1240)).toBe(1.2);
        expect(seconds(1250)).toBe(1.3);
        expect(seconds(0)).toBe(0);
    });
});

describe("formatSeconds", () => {
    it("writes seconds with one decimal", () => {
        expect(formatSeconds(9800)).toBe("9.8 s");
        expect(formatSeconds(109_050)).toBe("109.1 s");
    });
});
