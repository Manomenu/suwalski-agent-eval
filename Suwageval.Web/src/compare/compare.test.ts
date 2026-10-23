import { describe, expect, it } from "vitest";

import {
    changeBackground,
    changeOfTrend,
    checkCellChange,
    checksChange,
    samplesChange,
    scenarioCellChange,
    shareChange,
    BETTER_BACKGROUND,
    WORSE_BACKGROUND,
    baselineOf,
    deltaText,
    searchWithBaseline,
    timeDeltaText,
    toggledBaseline,
    trendColor,
} from "./compare";
import type { VariantCell, VariantCheck } from "./api";

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

const cell = (checksPassed: number, checksTotal: number): VariantCell => ({
    checksPassed,
    checksTotal,
    failedChecks: [],
    checks: [],
    samples: 1,
    samplesFailed: 0,
    noAnswers: [],
});
const check = (passRate: number): VariantCheck => ({
    key: "k",
    passed: passRate === 1,
    passRate,
    samples: 1,
    samplesPassed: 1,
    judgedByModel: false,
    failures: [],
});

describe("shareChange", () => {
    it("is better, worse or the same, and none without both shares", () => {
        expect(shareChange(0.8, 0.5)).toBe("better");
        expect(shareChange(0.2, 0.5)).toBe("worse");
        expect(shareChange(0.5, 0.5)).toBe("same");
        expect(shareChange(null, 0.5)).toBeNull();
        expect(shareChange(0.5, undefined)).toBeNull();
    });
});

describe("scenarioCellChange", () => {
    it("compares the share of checks passed against the baseline's cell", () => {
        expect(scenarioCellChange(cell(2, 2), cell(1, 2))).toBe("better");
        expect(scenarioCellChange(cell(0, 2), cell(1, 2))).toBe("worse");
        expect(scenarioCellChange(cell(1, 2), cell(2, 4))).toBe("same");
    });
    it("is none when a cell is missing or has no check", () => {
        expect(scenarioCellChange(null, cell(1, 2))).toBeNull();
        expect(scenarioCellChange(cell(1, 2), null)).toBeNull();
        expect(scenarioCellChange(cell(0, 0), cell(1, 2))).toBeNull();
    });
});

describe("checkCellChange", () => {
    it("takes the server's trend, else compares the pass rates", () => {
        const compared = { cells: [{ trend: "Worse" }] } as unknown as Parameters<typeof checkCellChange>[2];
        expect(checkCellChange(check(1), check(0), compared, 0)).toBe("worse");
        expect(checkCellChange(check(1), check(0), undefined, 0)).toBe("better");
        expect(checkCellChange(check(0), check(1), undefined, 0)).toBe("worse");
        expect(checkCellChange(check(1), null, undefined, 0)).toBeNull();
        expect(checkCellChange(null, check(1), undefined, 0)).toBeNull();
    });
});

describe("summary changes", () => {
    it("compares the share of checks passed", () => {
        expect(checksChange({ checksPassed: 3, checksFailed: 0 }, { checksPassed: 2, checksFailed: 1 })).toBe("better");
        expect(checksChange({ checksPassed: 1, checksFailed: 2 }, { checksPassed: 2, checksFailed: 1 })).toBe("worse");
        expect(checksChange({ checksPassed: 0, checksFailed: 0 }, { checksPassed: 2, checksFailed: 1 })).toBeNull();
    });
    it("compares the share of samples answered", () => {
        expect(samplesChange({ samplesDone: 4, samplesFailed: 0 }, { samplesDone: 4, samplesFailed: 1 })).toBe("better");
        expect(samplesChange({ samplesDone: 4, samplesFailed: 1 }, { samplesDone: 4 })).toBe("worse");
        expect(samplesChange({ samplesDone: 4 }, { samplesDone: 4, samplesFailed: 0 })).toBe("same");
    });
});

describe("changeBackground / changeOfTrend", () => {
    it("colours only better and worse", () => {
        expect(changeBackground("better")).toBe(BETTER_BACKGROUND);
        expect(changeBackground("worse")).toBe(WORSE_BACKGROUND);
        expect(changeBackground("same")).toBe("transparent");
        expect(changeBackground(null)).toBe("transparent");
        expect(changeOfTrend("Better")).toBe("better");
        expect(changeOfTrend("Same")).toBe("same");
        expect(changeOfTrend(null)).toBeNull();
    });
});
