import { describe, expect, it } from "vitest";

import { formatTime } from "./time";

describe("formatTime", () => {
    it("is a dash for no time", () => {
        expect(formatTime(null)).toBe("—");
        expect(formatTime(undefined)).toBe("—");
        expect(formatTime("")).toBe("—");
    });

    it("writes a time as a local date and time", () => {
        expect(formatTime("2026-10-01T10:00:00Z")).toContain("2026");
    });
});
