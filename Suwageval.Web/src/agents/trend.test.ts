import { describe, expect, it } from "vitest";

import type { RunPassRates } from "./api";
import {
    ALL_SERIES,
    DETAILS_KEY,
    LABEL_KEY,
    RECHECK_KEY,
    RUN_KEY,
    asTrendRow,
    hasRechecks,
    legendColor,
    percent,
    seriesColor,
    toggleHidden,
    trendRows,
    trendSeries,
} from "./trend";

function point(
    id: string,
    passRate: number,
    tags: [string, number][],
    versionLabel: string | null = "v1",
    tag: string | null = null,
): RunPassRates {
    return {
        runId: id,
        tag,
        createdAt: "2026-10-16T14:05:00Z",
        agentVersionId: "version",
        versionLabel,
        modelNames: ["big", "small"],
        passRate,
        tags: tags.map(([tag, rate]) => ({ tag, passRate: rate })),
    };
}

const points = [point("r1", 0.5, [["smoke", 1]]), point("r2", 2 / 3, [["regression", 0.25]], null)];

describe("trendSeries", () => {
    it("is All, then the tags of every run, sorted", () => {
        expect(trendSeries(points)).toEqual([ALL_SERIES, "regression", "smoke"]);
    });
});

describe("trendRows", () => {
    it("gives a percentage per series rounded to one decimal, null where a run has no such tag", () => {
        const rows = trendRows(points);

        expect(rows.map((row) => [row[RUN_KEY], row[ALL_SERIES], row["smoke"], row["regression"]])).toEqual([
            ["r1", 50, 100, null],
            ["r2", 66.7, null, 25],
        ]);
    });

    it("names the version and the models for the tooltip", () => {
        const rows = trendRows(points);

        expect(rows.map((row) => row[DETAILS_KEY])).toEqual(["v1 · big, small", "no label · big, small"]);
        expect(rows[0]?.[LABEL_KEY]).toMatch(/^\d{2}\/\d{2} \d{2}:\d{2}$/);
    });
});

describe("percent", () => {
    it("rounds to one decimal", () => {
        expect(percent(0.12345)).toBe(12.3);
        expect(percent(1)).toBe(100);
    });
});

describe("toggleHidden", () => {
    it("hides a shown series and shows a hidden one, leaving the input alone", () => {
        const hidden = new Set<string>();
        const once = toggleHidden(hidden, "smoke");

        expect([...once]).toEqual(["smoke"]);
        expect(hidden.size).toBe(0);
        expect(toggleHidden(once, "smoke").size).toBe(0);
    });
});

describe("seriesColor", () => {
    it("is mauve for All and a theme accent per tag, repeating after the palette", () => {
        const series = [ALL_SERIES, ...Array.from({ length: 9 }, (_, i) => `t${String(i)}`)];

        expect(seriesColor(ALL_SERIES, series)).toBe("mauve.5");
        expect(seriesColor("t0", series)).toBe("blue.5");
        expect(seriesColor("t1", series)).toBe("green.5");
        expect(seriesColor("t7", series)).toBe("blue.5");
    });
});

describe("asTrendRow", () => {
    it("accepts a row and nothing else", () => {
        expect(asTrendRow({ [RUN_KEY]: "r1" })).not.toBeNull();
        expect(asTrendRow({ other: 1 })).toBeNull();
        expect(asTrendRow(undefined)).toBeNull();
    });
});

describe("All", () => {
    it("comes only from runs of every scenario; a run limited to a tag adds to that tag's line only", () => {
        const rows = trendRows([point("full", 0.9, [["smoke", 1]]), point("smoke-only", 0.5, [["smoke", 0.5]], "v1", "smoke")]);
        expect(rows.map((row) => row[ALL_SERIES])).toEqual([90, null]);
        expect(rows.map((row) => row["smoke"])).toEqual([100, 50]);
    });

    it("takes the legend's colour without the shade", () => {
        expect(legendColor("smoke", [ALL_SERIES, "smoke"])).toBe("blue");
        expect(legendColor(ALL_SERIES, [ALL_SERIES])).toBe("mauve");
    });
});

describe("re-checked points", () => {
    it("carry the re-check in their row and are announced by hasRechecks", () => {
        const rechecked = [{ ...point("r1", 0.5, []), recheckedBy: "r1-recheck" }, point("r2", 1, [])];

        expect(trendRows(rechecked).map((row) => row[RECHECK_KEY])).toEqual(["r1-recheck", null]);
        expect(hasRechecks(rechecked)).toBe(true);
        expect(hasRechecks([point("r2", 1, [])])).toBe(false);
    });
});
