import { describe, expect, it } from "vitest";

import { changeMarks, markLines, seriesColor, swatchColor } from "./chartStyle";

describe("seriesColor", () => {
    it("gives the main series the theme's colour and the others the accents, repeating", () => {
        expect(seriesColor(0)).toBe("mauve.5");
        expect(seriesColor(1)).toBe("blue.5");
        expect(seriesColor(2)).toBe("green.5");
        expect(seriesColor(8)).toBe("blue.5");
    });

    it("drops the shade for a swatch", () => {
        expect(swatchColor("blue.5")).toBe("blue");
    });
});

describe("markLines", () => {
    it("draws every mark as a dashed line with its label", () => {
        expect(markLines([{ x: "2026-10-24", label: "version v7" }])).toEqual([
            { x: "2026-10-24", label: "version v7", labelPosition: "insideTopLeft", color: "gray.6", strokeDasharray: "4 4" },
        ]);
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
