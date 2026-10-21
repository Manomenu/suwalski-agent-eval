import { describe, expect, it } from "vitest";

import { baselineFirst, deltaText, timeDeltaText, trendColor } from "./compare";

describe("baselineFirst", () => {
    it("puts the baseline first and keeps the others in order", () => {
        expect(baselineFirst(["a", "b", "c"], "b")).toEqual(["b", "a", "c"]);
        expect(baselineFirst(["a", "b"], "a")).toEqual(["a", "b"]);
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
