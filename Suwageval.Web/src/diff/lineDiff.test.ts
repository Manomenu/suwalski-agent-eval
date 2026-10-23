import { describe, expect, it } from "vitest";

import { diffBlocks, diffRows, blocksFrom, type DiffBlock } from "./lineDiff";

describe("diffRows", () => {
    it("numbers the lines of each side", () => {
        expect(diffRows("a\nb\nc\n", "a\nx\nc\n")).toEqual([
            { kind: "Same", text: "a", fromLine: 1, toLine: 1 },
            { kind: "Removed", text: "b", fromLine: 2, toLine: null },
            { kind: "Added", text: "x", fromLine: null, toLine: 2 },
            { kind: "Same", text: "c", fromLine: 3, toLine: 3 },
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
        expect(blocks.map((block) => [block.folded, block.rows.length])).toEqual([
            [true, 7],
            [false, 3],
            [false, 2],
            [false, 3],
            [true, 7],
        ]);
        expect(blocks[1]?.rows.map((row) => row.text)).toEqual(["top7", "top8", "top9"]);
    });

    it("folds the run between two changes only past 6 lines, and never a single line", () => {
        const make = (middle: number) => diffBlocks(diffRows(`x\n${lines(middle, "m")}y\n`, `X\n${lines(middle, "m")}Y\n`));
        expect(make(6).some((block) => block.folded)).toBe(false);
        expect(make(7).some((block) => block.folded)).toBe(false);
        const folded = make(8).filter((block) => block.folded);
        expect(folded.map((block) => block.rows.length)).toEqual([2]);
    });
});

describe("blocksFrom", () => {
    const blocks: DiffBlock[] = [
        {
            folded: false,
            rows: [
                { kind: "Same", text: "agents:", fromLine: 1, toLine: 1 },
                { kind: "Removed", text: "Keep answers short.", fromLine: 2 },
                { kind: "Added", text: "Answer in one sentence.", toLine: 2 },
            ],
        },
    ];

    it("keeps the server's diff when it is asked from the version the server took as the base", () => {
        expect(blocksFrom({ from: { id: "old" }, blocks }, "old")).toEqual(blocks);
    });

    it("turns it around when asked from the other version: added and removed swap, so do the line numbers", () => {
        expect(blocksFrom({ from: { id: "old" }, blocks }, "new")).toEqual([
            {
                folded: false,
                rows: [
                    { kind: "Same", text: "agents:", fromLine: 1, toLine: 1 },
                    { kind: "Removed", text: "Answer in one sentence.", fromLine: 2, toLine: null },
                    { kind: "Added", text: "Keep answers short.", fromLine: null, toLine: 2 },
                ],
            },
        ]);
    });
});
