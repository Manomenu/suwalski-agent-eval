import { describe, expect, it } from "vitest";

import { runPrintLine } from "./printLine";

const run = { id: "r1", agent: "support" };

describe("runPrintLine", () => {
    it("says all scenarios without a filter", () => {
        expect(runPrintLine(run, [], false)).toBe("Run r1 of support · all scenarios");
    });
    it("names the selected tags and the hidden passed checks", () => {
        expect(runPrintLine(run, ["smoke", "billing"], true)).toBe(
            "Run r1 of support · scenarios with tags: smoke, billing · passed checks hidden",
        );
    });
});
