// A line diff of two texts, as plain functions: every line with its numbers on each side, and the hunks a reader sees (changes
// with their context, long unchanged runs folded), in the shape the server's version diff has. Used for the checks a re-check
// changes; the diff of two agent versions comes from the server.
import { diffLines } from "diff";

import type { components } from "../api/openapi";

/** Unchanged lines kept around a change; a longer unchanged run folds. */
const DIFF_CONTEXT = 3;

export type DiffRow = components["schemas"]["DiffRow"];

/** Every line of both texts, in order, with its numbers on each side. */
export function diffRows(from: string, to: string): DiffRow[] {
    const rows: DiffRow[] = [];
    let fromLine = 1;
    let toLine = 1;
    for (const part of diffLines(from, to)) {
        const lines = part.value.split("\n");
        if (lines[lines.length - 1] === "") lines.pop();
        for (const text of lines) {
            if (part.added) rows.push({ kind: "Added", text, fromLine: null, toLine: toLine++ });
            else if (part.removed) rows.push({ kind: "Removed", text, fromLine: fromLine++, toLine: null });
            else rows.push({ kind: "Same", text, fromLine: fromLine++, toLine: toLine++ });
        }
    }
    return rows;
}

/** A stretch of the diff: lines shown, or unchanged lines folded away until the reader opens them. */
export type DiffBlock = components["schemas"]["DiffBlock"];

/**
 * The hunks: every change with `context` unchanged lines around it. A longer unchanged run folds into one block (a run that
 * would hide a single line stays as it is); texts with no change give no blocks at all.
 */
export function diffBlocks(rows: DiffRow[], context = DIFF_CONTEXT): DiffBlock[] {
    if (rows.every((row) => row.kind === "Same")) return [];
    const blocks: DiffBlock[] = [];
    let index = 0;
    while (index < rows.length) {
        const start = index;
        const unchanged = rows[index]?.kind === "Same";
        while (index < rows.length && (rows[index]?.kind === "Same") === unchanged) index++;
        const run = rows.slice(start, index);
        const keepBefore = start === 0 ? 0 : context;
        const keepAfter = index === rows.length ? 0 : context;
        const hidden = run.length - keepBefore - keepAfter;
        if (!unchanged || hidden < 2) {
            blocks.push({ folded: false, rows: run });
            continue;
        }
        if (keepBefore > 0) blocks.push({ folded: false, rows: run.slice(0, keepBefore) });
        blocks.push({ folded: true, rows: run.slice(keepBefore, run.length - keepAfter) });
        if (keepAfter > 0) blocks.push({ folded: false, rows: run.slice(run.length - keepAfter) });
    }
    return blocks;
}

/** The same diff read the other way round: added lines become removed ones and back, the line numbers swap sides, and in each
 * run of changes the removed lines come first again (as a diff is read). */
function reversedBlocks(blocks: readonly DiffBlock[]): DiffBlock[] {
    return blocks.map((block) => {
        const swapped = block.rows.map((row): DiffRow => ({
            ...row,
            kind: row.kind === "Added" ? "Removed" : row.kind === "Removed" ? "Added" : row.kind,
            fromLine: row.toLine ?? null,
            toLine: row.fromLine ?? null,
        }));
        const rows: DiffRow[] = [];
        let run: DiffRow[] = [];
        const flush = () => {
            rows.push(...run.filter((row) => row.kind === "Removed"), ...run.filter((row) => row.kind === "Added"));
            run = [];
        };
        for (const row of swapped) {
            if (row.kind === "Same") {
                flush();
                rows.push(row);
            } else {
                run.push(row);
            }
        }
        flush();
        return { ...block, rows };
    });
}

/** A version diff's blocks read from `fromVersionId`: the server puts the older version first, so a diff asked from the newer one is
 * turned around. */
export function blocksFrom(diff: { from: { id: string }; blocks: readonly DiffBlock[] }, fromVersionId: string): DiffBlock[] {
    return diff.from.id === fromVersionId ? [...diff.blocks] : reversedBlocks(diff.blocks);
}
