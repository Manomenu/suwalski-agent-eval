import { describe, expect, it } from "vitest";

import type { CheckRunResult, ScenarioRunResult } from "../runs/api";
import {
    alignRows,
    cellOf,
    checkOfRow,
    cellText,
    cellTone,
    checksPassedText,
    delta,
    deltaText,
    parseRunIds,
    passRateOf,
    rowComparable,
    rowDiffers,
    sameAgent,
    tagRows,
    TIME_SAME_MS,
    timeDelta,
    timeDeltaText,
    trend,
    trendColor,
    trendMarker,
    visibleRows,
    type CheckCell,
} from "./compare";

const A = "11111111-2222-3333-4444-555555555555";
const B = "11111111-2222-3333-4444-666666666666";

/** A check with `passes` of `samples` samples passed, against a threshold of 1 unless given. */
function check(key: string, passes: number, samples: number, threshold = 1): CheckRunResult {
    return {
        key,
        revision: 1,
        latestRevision: 1,
        judgedByModel: false,
        definition: { type: "text", key, scenarioKey: "s", contains: "x" },
        passed: passes / samples >= threshold,
        passRate: passes / samples,
        passThreshold: threshold,
        samples: Array.from({ length: samples }, (_, index) => ({
            sample: index + 1,
            passed: index < passes,
            notJudged: false,
            evidence: [],
            message: "",
        })),
    };
}

function scenario(key: string, ...checks: CheckRunResult[]): ScenarioRunResult {
    return { key, revision: 1, samples: [], checks };
}

describe("checkOfRow", () => {
    const row = { scenarioKey: "s", checkKey: "c", cells: [] };
    const run = (...checks: CheckRunResult[]) => ({ scenarios: [scenario("s", ...checks)] });

    it("takes the baseline's check, else the first run that has it", () => {
        const runs = [run(check("c", 1, 1)), run({ ...check("c", 1, 1), revision: 2 })];
        expect(checkOfRow(runs, row, 1)?.revision).toBe(2);
        expect(checkOfRow([run(), run({ ...check("c", 1, 1), revision: 2 })], row, 0)?.revision).toBe(2);
        expect(checkOfRow([run(), run()], row, 0)).toBeNull();
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

describe("sameAgent", () => {
    it("is true only when every run is of one agent", () => {
        expect(sameAgent([{ run: { agent: "a" } }, { run: { agent: "a" } }])).toBe(true);
        expect(sameAgent([{ run: { agent: "a" } }, { run: { agent: "b" } }])).toBe(false);
    });
});

describe("cellOf", () => {
    it("takes the verdict, the pass rate and the samples that passed", () => {
        expect(cellOf(check("c", 3, 5))).toEqual({ passed: false, passRate: 0.6, passes: 3, samples: 5, notJudged: 0 });
        expect(cellOf(check("c", 3, 5, 0.6))).toEqual({ passed: true, passRate: 0.6, passes: 3, samples: 5, notJudged: 0 });
    });
});

describe("alignRows", () => {
    const baseline = { scenarios: [scenario("s1", check("a", 1, 1), check("b", 0, 1))] };
    const other = { scenarios: [scenario("s1", check("b", 1, 1), check("c", 1, 1)), scenario("s2", check("a", 1, 1))] };

    it("lines checks up by scenario key and check key, in order of appearance", () => {
        const rows = alignRows([baseline, other]);

        expect(rows.map((row) => `${row.scenarioKey}/${row.checkKey}`)).toEqual(["s1/a", "s1/b", "s1/c", "s2/a"]);
        expect(rows[0]?.cells.map((cell) => cell?.passed ?? null)).toEqual([true, null]);
        expect(rows[1]?.cells.map((cell) => cell?.passed ?? null)).toEqual([false, true]);
        expect(rows[3]?.cells.map((cell) => cell?.passed ?? null)).toEqual([null, true]);
    });

    it("does not mix the same check key of two scenarios", () => {
        expect(alignRows([baseline, other]).filter((row) => row.checkKey === "a")).toHaveLength(2);
    });
});

describe("differences", () => {
    const pass = cellOf(check("c", 1, 1));
    const fail = cellOf(check("c", 0, 1));
    const half = cellOf(check("c", 1, 2, 0.5));
    const row = (...cells: (CheckCell | null)[]) => ({ scenarioKey: "s", checkKey: "c", cells });

    it("a row differs when the verdict or the pass rate differs", () => {
        expect(rowDiffers(row(pass, pass))).toBe(false);
        expect(rowDiffers(row(pass, fail))).toBe(true);
        expect(rowDiffers(row(pass, half))).toBe(true);
        expect(rowDiffers(row(half, half, half))).toBe(false);
    });

    it("a check missing from some runs only is a difference; missing from all is not", () => {
        expect(rowDiffers(row(pass, null))).toBe(true);
        expect(rowDiffers(row(null, pass))).toBe(true);
        expect(rowDiffers(row(null, null))).toBe(false);
    });

    it("is comparable when every run has it", () => {
        expect(rowComparable(row(pass, fail))).toBe(true);
        expect(rowComparable(row(pass, null))).toBe(false);
    });

    it("the switches combine", () => {
        const same = row(pass, pass);
        const worse = row(pass, fail);
        const missing = row(pass, null);
        const allMissing = row(null, null);
        const rows = [same, worse, missing, allMissing];

        expect(visibleRows(rows, { onlyDifferences: false, hideUncomparable: false })).toEqual(rows);
        expect(visibleRows(rows, { onlyDifferences: true, hideUncomparable: false })).toEqual([worse, missing]);
        expect(visibleRows(rows, { onlyDifferences: false, hideUncomparable: true })).toEqual([same, worse]);
        expect(visibleRows(rows, { onlyDifferences: true, hideUncomparable: true })).toEqual([worse]);
    });
});

describe("a check with samples nobody could judge", () => {
    const notJudged = {
        ...check("judged", 0, 2),
        samples: [
            { sample: 1, passed: false, notJudged: true, evidence: [], message: "Not judged: the model did not answer." },
            { sample: 2, passed: false, notJudged: false, evidence: [], message: "" },
        ],
    };

    it("is yellow, not red, and says how many were not judged", () => {
        const cell = cellOf(notJudged);
        expect(cell.notJudged).toBe(1);
        expect(cellTone(cell, "same").color).toBe("yellow");
        expect(cellText(cell).samples).toBe("0/2 · 1 not judged");
    });
});

describe("trend, tone and text", () => {
    const pass = cellOf(check("c", 3, 3));
    const fail = cellOf(check("c", 0, 3));
    const partly = cellOf(check("c", 1, 3));
    const mostly = cellOf(check("c", 2, 3));

    it("is better or worse by the verdict first, then by the pass rate", () => {
        expect(trend(pass, fail)).toBe("better");
        expect(trend(fail, pass)).toBe("worse");
        expect(trend(mostly, partly)).toBe("better");
        expect(trend(partly, mostly)).toBe("worse");
        expect(trend(pass, pass)).toBe("same");
    });

    it("has nothing to say about a check that one of the runs lacks", () => {
        expect(trend(null, pass)).toBe("none");
        expect(trend(pass, null)).toBe("none");
    });

    it("colours by the verdict and frames what changed", () => {
        expect(cellTone(pass, "same")).toEqual({ color: "green", emphasised: false });
        expect(cellTone(fail, "worse")).toEqual({ color: "red", emphasised: true });
        expect(cellTone(pass, "better")).toEqual({ color: "green", emphasised: true });
        expect(cellTone(null, "none")).toEqual({ color: "gray", emphasised: false });
    });

    it("marks and colours the trend", () => {
        expect([trendMarker("better"), trendMarker("worse"), trendMarker("same"), trendMarker("none")]).toEqual(["▲", "▼", "", ""]);
        expect([trendColor("better"), trendColor("worse"), trendColor("same")]).toEqual(["green", "red", "dimmed"]);
    });

    it("writes the pass rate and the samples, or a dash", () => {
        expect(cellText(cellOf(check("c", 3, 5)))).toEqual({ rate: "60%", samples: "3/5" });
        expect(cellText(null)).toEqual({ rate: "—", samples: "" });
    });
});

describe("pass rate per tag", () => {
    const refund = scenario("refund", check("called", 1, 1), check("polite", 0, 1));
    const greet = scenario("greet", check("hello", 1, 1));
    const gone = scenario("gone", check("old", 0, 1));
    const tags = new Map([
        ["refund", ["billing", "critical"]],
        ["greet", ["smoke"]],
    ]);

    it("is the share of passed checks over the scenarios of the tag, a check counting once", () => {
        expect(passRateOf([refund])).toBe(0.5);
        expect(passRateOf([refund, greet])).toBeCloseTo(2 / 3);
        expect(passRateOf([])).toBeNull();
    });

    it("has an All row first, then the tags sorted; a scenario without tags is only in All", () => {
        const rows = tagRows([{ scenarios: [refund, greet, gone] }], tags);

        expect(rows.map((row) => row.tag)).toEqual([null, "billing", "critical", "smoke"]);
        expect(rows[0]?.rates[0]).toBeCloseTo(0.5);
        expect(rows[1]?.rates).toEqual([0.5]);
        expect(rows[3]?.rates).toEqual([1]);
    });

    it("has no rate for a run without a check of that tag", () => {
        const rows = tagRows([{ scenarios: [refund] }, { scenarios: [greet] }], tags);

        expect(rows.find((row) => row.tag === "billing")?.rates).toEqual([0.5, null]);
        expect(rows.find((row) => row.tag === "smoke")?.rates).toEqual([null, 1]);
    });
});

describe("delta", () => {
    it("is in percentage points, rounded to one decimal", () => {
        expect(delta(0.75, 0.5)).toEqual({ points: 25, trend: "better" });
        expect(delta(0.5, 2 / 3)).toEqual({ points: -16.7, trend: "worse" });
        expect(delta(0.5, 0.5)).toEqual({ points: 0, trend: "same" });
    });

    it("is the same when the change rounds to nothing", () => {
        expect(delta(0.5001, 0.5)?.trend).toBe("same");
    });

    it("is none when either run has no such checks", () => {
        expect(delta(null, 0.5)).toBeNull();
        expect(delta(0.5, null)).toBeNull();
    });

    it("is written with an arrow", () => {
        expect(deltaText({ points: 25, trend: "better" })).toBe("▲ 25 pp");
        expect(deltaText({ points: -16.7, trend: "worse" })).toBe("▼ 16.7 pp");
        expect(deltaText({ points: 0, trend: "same" })).toBe("±0 pp");
    });
});

describe("checksPassedText", () => {
    it("is passed over all checks", () => {
        expect(checksPassedText({ checksPassed: 7, checksFailed: 3 })).toBe("7/10 checks passed");
    });
});

describe("timeDelta", () => {
    it("is better when faster and worse when slower: lower is better", () => {
        expect(timeDelta(8000, 10_000)).toEqual({ milliseconds: -2000, trend: "better" });
        expect(timeDelta(12_000, 10_000)).toEqual({ milliseconds: 2000, trend: "worse" });
    });

    it("is the same under 5 % of the baseline or under the noise floor", () => {
        expect(timeDelta(10_400, 10_000)?.trend).toBe("same");
        expect(timeDelta(10_500, 10_000)?.trend).toBe("worse");
        expect(timeDelta(400 + TIME_SAME_MS - 1, 400)?.trend).toBe("same");
        expect(timeDelta(400 + TIME_SAME_MS, 400)?.trend).toBe("worse");
    });

    it("is none when either run made no call", () => {
        expect(timeDelta(null, 1000)).toBeNull();
        expect(timeDelta(1000, null)).toBeNull();
    });

    it("is written with an arrow that follows the time", () => {
        expect(timeDeltaText({ milliseconds: -1200, trend: "better" })).toBe("▼ 1.2 s");
        expect(timeDeltaText({ milliseconds: 3000, trend: "worse" })).toBe("▲ 3.0 s");
        expect(timeDeltaText({ milliseconds: 30, trend: "same" })).toBe("±0 s");
    });
});
