import { describe, expect, it } from "vitest";

import { changeMarks, seriesColor } from "./chartStyle";

describe("seriesColor", () => {
    it("gives the main series the theme's colour and the others the accents, repeating", () => {
        expect(seriesColor(0)).toBe("var(--ctp-mauve)");
        expect(seriesColor(1)).toBe("var(--ctp-blue)");
        expect(seriesColor(2)).toBe("var(--ctp-green)");
        expect(seriesColor(8)).toBe("var(--ctp-blue)");
    });
});

describe("changeMarks", () => {
    it("names a version by its label or the start of its id and data by its short version, joined per point", () => {
        expect(
            changeMarks([
                { x: "r1", version: { agentVersionId: "aaaaaaaa-1111", label: "v7" } },
                { x: "r2", version: { agentVersionId: "bbbbbbbb-2222", label: null } },
                { x: "r2", dataVersion: "93a4af9723bf03c6f0d0d6825d1e7786" },
            ]),
        ).toEqual([
            { x: "r1", label: "version v7" },
            { x: "r2", label: "version bbbbbbbb, data 93a4af97…" },
        ]);
    });
});
