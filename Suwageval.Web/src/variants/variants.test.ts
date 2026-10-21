import { describe, expect, it } from "vitest";

import type { VariantCell, VariantScenario } from "./api";
import {
    bestColumns,
    cellText,
    columnTotals,
    noAnswerText,
    scenarioSections,
    selectionOf,
    variantName,
    VARIANTS_MAX_RUNS,
} from "./variants";

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
        const many = Array.from({ length: VARIANTS_MAX_RUNS + 1 }, (_, index) => `${ID.slice(0, -2)}${String(10 + index)}`).join(",");
        expect(selectionOf(many, "", "").kind).toBe("error");
    });
});

describe("variantName", () => {
    const run = { agentVersionId: ID };
    it("is the label of the version, else the start of its id", () => {
        expect(variantName({ versionLabel: "gpt-4o", run } as never)).toBe("gpt-4o");
        expect(variantName({ versionLabel: " ", run } as never)).toBe(ID.slice(0, 8));
        expect(variantName({ versionLabel: "gpt-4o", run: { ...run, label: "relabelled" } } as never)).toBe("relabelled");
    });
});

const cell = (checksPassed: number, checksTotal: number, samplesFailed = 0): VariantCell => ({
    checksPassed,
    checksTotal,
    failedChecks: [],
    samples: 4,
    samplesFailed,
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
