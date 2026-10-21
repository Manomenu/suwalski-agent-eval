import { describe, expect, it } from "vitest";

import type { ComparedCell, ComparedCheck } from "./api";
import {
    baselineFirst,
    cellText,
    columnChecksCounts,
    cellTone,
    deltaText,
    parseRunIds,
    timeDeltaText,
    trendColor,
    trendMarker,
} from "./compare";

const A = "11111111-2222-3333-4444-555555555555";
const B = "11111111-2222-3333-4444-666666666666";

/** What the server sends for a check with `passes` of `samples` samples passed. */
function cell(passes: number, samples: number, over: Partial<ComparedCell> = {}): ComparedCell {
    return {
        passed: passes === samples,
        passRate: passes / samples,
        samplesPassed: passes,
        samples,
        notJudged: 0,
        revision: 1,
        latestRevision: 1,
        judgedByModel: false,
        ...over,
    };
}

describe("columnChecksCounts", () => {
    const row = (...cells: (ComparedCell | null)[]) => ({ cells }) as ComparedCheck;

    it("counts the checks one run has among the rows, a run without a check adding nothing", () => {
        const rows = [row(cell(1, 1), cell(0, 2)), row(cell(1, 1), null), row(cell(0, 1), cell(2, 2))];

        expect(columnChecksCounts(rows, 0)).toEqual({ checksPassed: 2, checksFailed: 1 });
        expect(columnChecksCounts(rows, 1)).toEqual({ checksPassed: 1, checksFailed: 1 });
    });
});

describe("parseRunIds", () => {
    it("reads two to five ids in the order given, each once", () => {
        expect(parseRunIds(`${A},${B}`)).toEqual({ kind: "ok", ids: [A, B] });
        expect(parseRunIds(` ${B} , ${A},${B}`)).toEqual({ kind: "ok", ids: [B, A] });
    });

    it("refuses fewer than two and more than five", () => {
        expect(parseRunIds("")).toEqual({ kind: "error", message: "Choose 2 to 5 runs to compare." });
        expect(parseRunIds(`${A},${A}`)).toEqual({ kind: "error", message: "Choose 2 to 5 runs to compare." });
        const six = Array.from({ length: 6 }, (_, index) => `11111111-2222-3333-4444-55555555555${index}`).join(",");
        expect(parseRunIds(six)).toEqual({ kind: "error", message: "Choose 2 to 5 runs to compare." });
    });

    it("refuses an id that is not a guid", () => {
        expect(parseRunIds(`${A},nope`)).toEqual({ kind: "error", message: '"nope" is not a run id.' });
    });
});

describe("baselineFirst", () => {
    it("puts the baseline first and keeps the others in order", () => {
        expect(baselineFirst(["a", "b", "c"], "b")).toEqual(["b", "a", "c"]);
        expect(baselineFirst(["a", "b"], "a")).toEqual(["a", "b"]);
    });
});

describe("a check with samples nobody could judge", () => {
    it("is yellow, not red, and says how many were not judged", () => {
        const notJudged = cell(0, 2, { notJudged: 1 });
        expect(cellTone(notJudged, "Same").color).toBe("yellow");
        expect(cellText(notJudged).samples).toBe("0/2 · 1 not judged");
    });
});

describe("tone and text", () => {
    it("colours by the verdict and frames what changed", () => {
        expect(cellTone(cell(3, 3), "Same")).toEqual({ color: "green", emphasised: false });
        expect(cellTone(cell(0, 3), "Worse")).toEqual({ color: "red", emphasised: true });
        expect(cellTone(cell(3, 3), "Better")).toEqual({ color: "green", emphasised: true });
        expect(cellTone(cell(3, 3), null)).toEqual({ color: "green", emphasised: false });
        expect(cellTone(null, null)).toEqual({ color: "gray", emphasised: false });
    });

    it("marks and colours the trend", () => {
        expect([trendMarker("Better"), trendMarker("Worse"), trendMarker("Same"), trendMarker(null)]).toEqual(["▲", "▼", "", ""]);
        expect([trendColor("Better"), trendColor("Worse"), trendColor("Same"), trendColor(undefined)]).toEqual([
            "green",
            "red",
            "dimmed",
            "dimmed",
        ]);
    });

    it("writes the pass rate and the samples, or a dash", () => {
        expect(cellText(cell(3, 5))).toEqual({ rate: "60%", samples: "3/5" });
        expect(cellText(null)).toEqual({ rate: "—", samples: "" });
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
