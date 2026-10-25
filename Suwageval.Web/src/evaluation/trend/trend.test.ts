import { describe, expect, it } from "vitest";

import {
    ALL_SERIES,
    DETAILS_KEY,
    LABEL_KEY,
    RECHECK_KEY,
    RUN_KEY,
    asTrendRow,
    hasRechecks,
    measuredSeries,
    recentTags,
    seriesColor,
    shownSeries,
    trendRows,
    trendSeries,
    turnTimeMeasure,
    runChanges,
    withTurnTimes,
} from "./trend";
import { type RunPassRates } from "./api";

function point(
    id: string,
    passRate: number,
    tags: [string, number][],
    versionLabel: string | null = "v1",
    limitedToTags: string[] = [],
    medianMs: number | null = null,
): RunPassRates {
    return {
        runId: id,
        limitedToTags,
        createdAt: "2026-10-16T14:05:00Z",
        agentVersionId: "version",
        versionLabel,
        modelNames: ["big", "small"],
        passRate,
        medianMs,
        p95Ms: medianMs === null ? null : medianMs * 3,
        tags: tags.map(([tag, rate]) => ({ tag, passRate: rate, medianMs: medianMs === null ? null : medianMs / 2, p95Ms: null })),
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

    it("names what the run covered, the version and the models for the tooltip", () => {
        const rows = trendRows(points);

        expect(rows.map((row) => row[DETAILS_KEY])).toEqual(["all scenarios · v1 · big, small", "all scenarios · no label · big, small"]);
        expect(rows[0]?.[LABEL_KEY]).toMatch(/^\d{2}\/\d{2} \d{2}:\d{2}$/);
    });
});

describe("recentTags", () => {
    const run = (id: string, ...tags: string[]) =>
        point(
            id,
            1,
            tags.map((tag) => [tag, 1]),
        );

    it("is every tag when there are fewer than the limit", () => {
        expect(recentTags([run("a", "billing"), run("b", "smoke")], 10).sort()).toEqual(["billing", "smoke"]);
    });

    it("takes the newest-used tags, newest first, and drops one used only long ago", () => {
        const points = [run("a", "old"), run("b", "t1", "t2"), run("c", "t3"), run("d", "t4", "t1")];

        expect(recentTags(points, 3)).toEqual(["t4", "t1", "t3"]);
    });
});

describe("shownSeries", () => {
    it("draws All and the recent tags with nothing picked, All and the picked tags otherwise", () => {
        const series = [ALL_SERIES, "billing", "old", "smoke"];

        expect(shownSeries(series, [], ["smoke", "billing"])).toEqual([ALL_SERIES, "billing", "smoke"]);
        expect(shownSeries(series, ["old", "gone"], ["smoke", "billing"])).toEqual([ALL_SERIES, "old"]);
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
        const rows = trendRows([point("full", 0.9, [["smoke", 1]]), point("smoke-only", 0.5, [["smoke", 0.5]], "v1", ["smoke"])]);
        expect(rows.map((row) => row[ALL_SERIES])).toEqual([90, null]);
        expect(rows.map((row) => row["smoke"])).toEqual([100, 50]);
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

describe("measuredSeries", () => {
    it("lists only the series the run measured, in the given order", () => {
        const [row] = trendRows([point("smoke-only", 0.5, [["smoke", 0.5]], "v1", ["smoke"])]);
        if (!row) throw new Error("one row");
        expect(measuredSeries(row, [ALL_SERIES, "regression", "smoke"])).toEqual(["smoke"]);
    });

    it("says whether the run covered every scenario or one tag", () => {
        const [full, tagged] = trendRows([point("full", 1, []), point("smoke-only", 1, [], "v1", ["smoke", "billing"])]);
        expect(full?.[DETAILS_KEY]).toMatch(/^all scenarios · /);
        expect(tagged?.[DETAILS_KEY]).toMatch(/^tags smoke, billing · /);
    });
});

describe("turn time", () => {
    const timed = [
        point("r1", 0.5, [["smoke", 1]], "v1", [], 1240),
        point("r2", 0.5, [["smoke", 1]], "v1", ["smoke"], 2000),
        point("r3", 1, []),
    ];

    it("keeps the runs that made a call", () => {
        expect(withTurnTimes(timed).map((run) => run.runId)).toEqual(["r1", "r2"]);
    });

    it("plots the median or the p95, All only from runs of every scenario, a tag from any run that has it", () => {
        const median = trendRows(withTurnTimes(timed), turnTimeMeasure("median"));
        const p95 = trendRows(withTurnTimes(timed), turnTimeMeasure("p95"));

        expect(median.map((row) => [row[ALL_SERIES], row["smoke"]])).toEqual([
            [1.2, 0.6],
            [null, 1],
        ]);
        expect(p95.map((row) => [row[ALL_SERIES], row["smoke"]])).toEqual([
            [3.7, null],
            [null, null],
        ]);
    });
});

describe("runChanges", () => {
    it("marks each charted run whose version differs from the run before it", () => {
        const runs = [
            { ...point("r1", 1, []), agentVersionId: "aaaaaaaa-1" },
            { ...point("r2", 1, []), agentVersionId: "aaaaaaaa-1" },
            { ...point("r3", 1, [], null), agentVersionId: "bbbbbbbb-2" },
        ];

        expect(runChanges(runs, trendRows(runs))).toEqual([{ x: "r3", version: { agentVersionId: "bbbbbbbb-2", label: null } }]);
    });

    it("marks new data against the last run that had any; a run without retrieval changes nothing", () => {
        const runs = [
            { ...point("r1", 1, []), dataVersion: "d1" },
            { ...point("r2", 1, []), dataVersion: null },
            { ...point("r3", 1, []), dataVersion: "d1" },
            { ...point("r4", 1, [], "v2"), agentVersionId: "other", dataVersion: "d2" },
        ];

        expect(runChanges(runs, trendRows(runs))).toEqual([
            { x: "r4", version: { agentVersionId: "other", label: "v2" }, dataVersion: "d2" },
        ]);
    });

    it("compares only the runs the chart shows", () => {
        const runs = [
            { ...point("r1", 1, []), agentVersionId: "a", dataVersion: "d1" },
            { ...point("r2", 1, []), agentVersionId: "b", dataVersion: "d2" },
            { ...point("r3", 1, []), agentVersionId: "a", dataVersion: "d1" },
        ];
        const charted = trendRows(runs).filter((row) => row[RUN_KEY] !== "r2");

        expect(runChanges(runs, charted)).toEqual([]);
    });
});
