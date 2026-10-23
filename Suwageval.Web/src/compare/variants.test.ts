import { describe, expect, it } from "vitest";

import type { VariantCell, VariantCheck, VariantScenario } from "./api";
import {
    bestColumns,
    cellStatus,
    cellText,
    checkMatrix,
    checkVerdictText,
    columnName,
    columnTitle,
    columnTotals,
    coverageHint,
    noAnswerRows,
    noAnswerText,
    runTagsText,
    scenarioDiffers,
    scenarioSections,
    selectionOf,
    visibleTagRows,
} from "./variants";
import { COMPARE_MAX_RUNS } from "../routes/links";

const ID = "3f2b8c1e-5d4a-4e6f-9a7b-0c1d2e3f4a5b";
const OTHER = "0a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d";

describe("selectionOf", () => {
    it("reads the runs, each once, in the order given", () => {
        expect(selectionOf(`${ID}, ${OTHER},${ID}`, "", "")).toEqual({ kind: "runs", ids: [ID, OTHER] });
    });

    it("reads a group, with the agent when one is named", () => {
        expect(selectionOf("", "sweep-1", "")).toEqual({ kind: "group", group: "sweep-1", agent: null });
        expect(selectionOf("", "sweep-1", "bot")).toEqual({ kind: "group", group: "sweep-1", agent: "bot" });
    });

    it("refuses both, neither, a word that is no run id and too many runs", () => {
        expect(selectionOf(ID, "sweep-1", "").kind).toBe("error");
        expect(selectionOf("", "", "").kind).toBe("error");
        expect(selectionOf("nope", "", "")).toEqual({ kind: "error", message: '"nope" is not a run id.' });
        const many = Array.from({ length: COMPARE_MAX_RUNS + 1 }, (_, index) => `${ID.slice(0, -2)}${String(10 + index)}`).join(",");
        expect(selectionOf(many, "", "").kind).toBe("error");
    });
});

describe("column names", () => {
    it("titles a column with the label of its run, or says it has none; the detail names it by the start of the id then", () => {
        const labelled = { run: { id: ID, label: "gpt-4o" } } as never;
        const unlabelled = { run: { id: ID, label: " " } } as never;

        expect([columnTitle(labelled), columnTitle(unlabelled)]).toEqual(["gpt-4o", "Unlabelled"]);
        expect([columnName(labelled), columnName(unlabelled)]).toEqual(["gpt-4o", `Run ${ID.slice(0, 8)}`]);
    });

    it("says what no tags means", () => {
        expect([runTagsText(["a", "b"]), runTagsText([]), runTagsText(undefined)]).toEqual(["a, b", "all scenarios", "all scenarios"]);
    });
});

const cell = (checksPassed: number, checksTotal: number, samplesFailed = 0): VariantCell => ({
    checksPassed,
    checksTotal,
    failedChecks: [],
    checks: [],
    samples: 4,
    samplesFailed,
    noAnswers: [],
});

const check = (key: string, passed: boolean, samplesPassed: number, samples: number): VariantCheck => ({
    key,
    passed,
    passRate: samplesPassed / samples,
    samples,
    judgedByModel: false,
    samplesPassed,
    failures: [],
});

describe("cellStatus", () => {
    it("says how a cell's checks came out, and no answer when no sample was answered", () => {
        expect(cellStatus(cell(2, 2))?.label).toBe("all passed");
        expect(cellStatus(cell(1, 2))?.label).toBe("some failed");
        expect(cellStatus(cell(0, 2))?.label).toBe("all failed");
        expect(cellStatus(cell(0, 0))?.label).toBe("no checks");
        expect(cellStatus(cell(0, 0, 4))?.label).toBe("no answer");
        expect(cellStatus(null)).toBeNull();
    });
});

describe("scenarioDiffers", () => {
    it("is true when the runs did not come out the same, a run without the scenario counting", () => {
        expect(scenarioDiffers([cell(2, 2), cell(2, 2)])).toBe(false);
        expect(scenarioDiffers([cell(2, 2), cell(1, 2)])).toBe(true);
        expect(scenarioDiffers([cell(2, 2), null])).toBe(true);
    });
});

describe("the detail of a scenario", () => {
    const first = { ...cell(1, 2), checks: [check("a", true, 3, 3), check("b", false, 0, 3)] };
    const second = { ...cell(1, 2), checks: [check("b", true, 2, 3)], noAnswers: [{ sample: 3, error: "HTTP 500" }] };

    it("lines the checks up by key across the columns, a column without the check getting a gap", () => {
        expect(checkMatrix([first, second, null]).map((row) => [row.key, row.checks.map((item) => item?.passed ?? null)])).toEqual([
            ["a", [true, null, null]],
            ["b", [false, true, null]],
        ]);
    });

    it("writes a verdict, with the samples when they were mixed", () => {
        expect([
            checkVerdictText(check("a", true, 3, 3)),
            checkVerdictText(check("b", false, 0, 3)),
            checkVerdictText(check("c", true, 2, 3)),
        ]).toEqual(["passed", "failed", "passed 2/3"]);
    });

    it("lists the samples without an answer by column", () => {
        expect(noAnswerRows([first, second])).toEqual([{ column: 1, sample: 3, error: "HTTP 500" }]);
    });
});

describe("cell texts", () => {
    it("counts the checks, and says what is missing", () => {
        expect(cellText(cell(3, 4))).toBe("3/4 checks");
        expect(cellText(cell(0, 0))).toBe("no checks");
        expect(cellText(null)).toBe("—");
    });

    it("names the samples without an answer", () => {
        expect(noAnswerText(cell(3, 4, 1))).toBe("1 of 4 samples: no answer");
        expect(noAnswerText(cell(3, 4))).toBeNull();
    });
});

describe("bestColumns", () => {
    it("marks the columns with the highest share", () => {
        expect(bestColumns([0.5, 0.75, null, 0.75])).toEqual(new Set([1, 3]));
    });

    it("marks nothing when the columns are equal or only one has a share", () => {
        expect(bestColumns([0.5, 0.5])).toEqual(new Set());
        expect(bestColumns([null, 1])).toEqual(new Set());
    });
});

describe("columnTotals", () => {
    const scenario = (...cells: (VariantCell | null)[]) => ({ key: "s", tags: [], cells }) as VariantScenario;

    it("adds up the checks and samples one run has in these scenarios, a run without the scenario adding nothing", () => {
        const scenarios = [scenario(cell(3, 4, 1), cell(1, 1)), scenario(cell(2, 2), null)];

        expect(columnTotals(scenarios, 0)).toEqual({ checksPassed: 5, checksFailed: 1, samplesTotal: 8, samplesDone: 8, samplesFailed: 1 });
        expect(columnTotals(scenarios, 1)).toEqual({ checksPassed: 1, checksFailed: 0, samplesTotal: 4, samplesDone: 4, samplesFailed: 0 });
    });
});

describe("scenarioSections", () => {
    const scenario = (key: string, tags: string[]): VariantScenario => ({ key, tags, cells: [] });
    const scenarios = [scenario("b", ["z", "a"]), scenario("a", []), scenario("c", ["m"]), scenario("d", ["a"])];

    it("is one section sorted by key when not grouped", () => {
        expect(scenarioSections(scenarios, false)).toEqual([
            { title: null, scenarios: [scenarios[1], scenarios[0], scenarios[2], scenarios[3]] },
        ]);
    });

    it("puts a scenario under its first selected tag when tags are selected", () => {
        expect(
            scenarioSections(scenarios, true, ["z", "m"]).map((section) => [section.title, section.scenarios.map((item) => item.key)]),
        ).toEqual([
            ["m", ["c"]],
            ["z", ["b"]],
            ["no tag", ["a", "d"]],
        ]);
    });

    it("puts each scenario under its first tag and those without a tag last", () => {
        expect(scenarioSections(scenarios, true).map((section) => [section.title, section.scenarios.map((item) => item.key)])).toEqual([
            ["a", ["b", "d"]],
            ["m", ["c"]],
            ["no tag", ["a"]],
        ]);
    });
});

describe("tag rows", () => {
    const row = (tag: string | null, ...rates: (number | null)[]) => ({
        tag,
        rates: rates.map((passRate) => ({ passRate, scenariosRan: 3, scenariosOfTag: 5 })),
    });

    it("leaves out a tag no run has a rate for and keeps the row of every check", () => {
        const rows = [row(null, null, null), row("mediacom", null, null), row("billing", null, 0.5)];

        expect(visibleTagRows(rows as never).map((item) => item.tag)).toEqual([null, "billing"]);
    });

    it("says how much of the tag ran where it has no rate", () => {
        expect(coverageHint("mediacom", { passRate: null, scenariosRan: 30, scenariosOfTag: 44 })).toBe(
            "Not every mediacom scenario ran (30 of 44)",
        );
        expect(coverageHint("mediacom", { passRate: 0.5, scenariosRan: 44, scenariosOfTag: 44 })).toBeNull();
        expect(coverageHint(null, { passRate: null })).toBeNull();
    });
});
