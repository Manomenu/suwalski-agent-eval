import { describe, expect, it } from "vitest";

import { COMPARE_MAX_RUNS } from "../../routes/links";
import { PRINT_COLUMN_WIDTH, PRINT_LABEL_COLUMN_WIDTH, PRINT_MAX_RUNS, PRINT_PAGE_WIDTH } from "./layout";
import { comparePrintLine, printMaxRuns } from "./printing";

describe("printMaxRuns", () => {
    it("counts the columns that fit after the label column", () => {
        expect(printMaxRuns(1000, 200, 100)).toBe(8);
        expect(printMaxRuns(1000, 250, 100)).toBe(7);
        expect(printMaxRuns(100, 200, 100)).toBe(0);
    });

    it("gives PRINT_MAX_RUNS for the widths of the printed table", () => {
        expect(printMaxRuns(PRINT_PAGE_WIDTH, PRINT_LABEL_COLUMN_WIDTH, PRINT_COLUMN_WIDTH)).toBe(PRINT_MAX_RUNS);
    });

    it("is the number of runs one comparison holds", () => {
        expect(COMPARE_MAX_RUNS).toBe(PRINT_MAX_RUNS);
    });
});

describe("comparePrintLine", () => {
    it("names the runs, the baseline and the filters", () => {
        expect(
            comparePrintLine({
                agent: "support",
                group: "sweep",
                runIds: ["1a2b3c4d-0000", "5e6f7a8b-0000"],
                baselineId: "1a2b3c4d-0000",
                selectedTags: ["smoke"],
                onlyDifferences: true,
                byTag: true,
            }),
        ).toBe(
            "Comparison of support · group sweep · runs 1a2b3c4d, 5e6f7a8b · baseline 1a2b3c4d · scenarios with tags: smoke · only rows that differ · listed by tag",
        );
    });
    it("says no baseline and all scenarios without them", () => {
        expect(
            comparePrintLine({
                agent: "a",
                group: null,
                runIds: ["abcdefgh1"],
                baselineId: null,
                selectedTags: [],
                onlyDifferences: false,
                byTag: false,
            }),
        ).toBe("Comparison of a · runs abcdefgh · no baseline · all scenarios");
    });
});
