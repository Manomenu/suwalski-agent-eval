import { describe, expect, it } from "vitest";

import { baselineOf, deltaText, searchWithBaseline, timeDeltaText, toggledBaseline, trendColor } from "./compare";

describe("baselineOf", () => {
    it("is the run the address names, or none", () => {
        expect(baselineOf("abc")).toBe("abc");
        expect(baselineOf(" abc ")).toBe("abc");
        expect(baselineOf("")).toBeNull();
    });
});

describe("toggledBaseline", () => {
    it("picks the clicked run and drops the baseline when it is clicked again", () => {
        expect(toggledBaseline(null, "a")).toBe("a");
        expect(toggledBaseline("a", "b")).toBe("b");
        expect(toggledBaseline("a", "a")).toBeNull();
    });
});

describe("searchWithBaseline", () => {
    it("sets, replaces and removes the baseline and keeps every other parameter", () => {
        expect(searchWithBaseline("?runs=a,b", "a")).toBe("?runs=a,b&baseline=a");
        expect(searchWithBaseline("?runs=a,b&baseline=a&tag=x", "b")).toBe("?runs=a,b&tag=x&baseline=b");
        expect(searchWithBaseline("?runs=a,b&baseline=a", null)).toBe("?runs=a,b");
        expect(searchWithBaseline("?baseline=a", null)).toBe("");
        expect(searchWithBaseline("", null)).toBe("");
    });
});

describe("trendColor", () => {
    it("colours better green and worse red, nothing else", () => {
        expect([trendColor("Better"), trendColor("Worse"), trendColor("Same"), trendColor(undefined)]).toEqual([
            "green",
            "red",
            "dimmed",
            "dimmed",
        ]);
    });
});

describe("deltaText", () => {
    it("is written with an arrow", () => {
        expect(deltaText({ points: 12.5, trend: "Better" })).toBe("▲ 12.5 pp");
        expect(deltaText({ points: -3, trend: "Worse" })).toBe("▼ 3 pp");
        expect(deltaText({ points: 0, trend: "Same" })).toBe("±0 pp");
    });
});

describe("timeDeltaText", () => {
    it("is written with an arrow that follows the time", () => {
        expect(timeDeltaText({ milliseconds: -1200, trend: "Better" })).toBe("▼ 1.2 s");
        expect(timeDeltaText({ milliseconds: 3000, trend: "Worse" })).toBe("▲ 3.0 s");
        expect(timeDeltaText({ milliseconds: 30, trend: "Same" })).toBe("±0 s");
    });
});
