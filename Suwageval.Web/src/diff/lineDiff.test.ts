import { describe, expect, it } from "vitest";

import { diffBlocks, diffRows } from "./lineDiff";

describe("diffRows", () => {
    it("numbers the lines of each side", () => {
        expect(diffRows("a\nb\nc\n", "a\nx\nc\n")).toEqual([
            { kind: "unchanged", text: "a", oldNumber: 1, newNumber: 1 },
            { kind: "removed", text: "b", oldNumber: 2, newNumber: null },
            { kind: "added", text: "x", oldNumber: null, newNumber: 2 },
            { kind: "unchanged", text: "c", oldNumber: 3, newNumber: 3 },
        ]);
    });
});

describe("diffBlocks", () => {
    const lines = (count: number, prefix: string) => Array.from({ length: count }, (_, index) => `${prefix}${String(index)}\n`).join("");

    it("gives no blocks for equal texts", () => {
        expect(diffBlocks(diffRows("a\nb\n", "a\nb\n"))).toEqual([]);
    });

    it("keeps 3 lines of context and folds the rest of a long unchanged run", () => {
        const rows = diffRows(`${lines(10, "top")}old\n${lines(10, "bottom")}`, `${lines(10, "top")}new\n${lines(10, "bottom")}`);
        const blocks = diffBlocks(rows);
        expect(blocks.map((block) => [block.kind, block.rows.length])).toEqual([
            ["folded", 7],
            ["lines", 3],
            ["lines", 2],
            ["lines", 3],
            ["folded", 7],
        ]);
        expect(blocks[1]?.rows.map((row) => row.text)).toEqual(["top7", "top8", "top9"]);
    });

    it("folds the run between two changes only past 6 lines, and never a single line", () => {
        const make = (middle: number) => diffBlocks(diffRows(`x\n${lines(middle, "m")}y\n`, `X\n${lines(middle, "m")}Y\n`));
        expect(make(6).some((block) => block.kind === "folded")).toBe(false);
        expect(make(7).some((block) => block.kind === "folded")).toBe(false);
        const folded = make(8).filter((block) => block.kind === "folded");
        expect(folded.map((block) => block.rows.length)).toEqual([2]);
    });
});
