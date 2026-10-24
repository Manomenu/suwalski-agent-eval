import { describe, expect, it } from "vitest";

import { PRINT_COLUMN_WIDTH, PRINT_LABEL_COLUMN_WIDTH, PRINT_MAX_RUNS, PRINT_PAGE_WIDTH } from "./layout";
import { comparePrintLine, printDisabledReason, printMaxRuns } from "./printing";

describe("printMaxRuns", () => {
    it("counts the columns that fit after the label column", () => {
        expect(printMaxRuns(1000, 200, 100)).toBe(8);
        expect(printMaxRuns(1000, 250, 100)).toBe(7);
        expect(printMaxRuns(100, 200, 100)).toBe(0);
    });

    it("gives PRINT_MAX_RUNS for the widths of the printed table", () => {
        expect(printMaxRuns(PRINT_PAGE_WIDTH, PRINT_LABEL_COLUMN_WIDTH, PRINT_COLUMN_WIDTH)).toBe(PRINT_MAX_RUNS);
    });
});

describe("printDisabledReason", () => {
    it("is undefined up to the cap and says the cap above it", () => {
        expect(printDisabledReason(PRINT_MAX_RUNS, PRINT_MAX_RUNS)).toBeUndefined();
        expect(printDisabledReason(PRINT_MAX_RUNS + 1, PRINT_MAX_RUNS)).toBe(
            `At most ${String(PRINT_MAX_RUNS)} runs fit a printed page — compare fewer runs, e.g. with the same baseline, and print each.`,
        );
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
