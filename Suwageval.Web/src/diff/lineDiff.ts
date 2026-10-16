// A line diff of two texts, as plain functions: every line with its numbers on each side, and the hunks a reader sees (changes
// with their context, long unchanged runs folded). Used for agent versions and for the checks a re-check changes.
import { diffLines } from "diff";

/** Unchanged lines kept around a change; a longer unchanged run folds. */
const DIFF_CONTEXT = 3;

export interface DiffRow {
    kind: "added" | "removed" | "unchanged";
    text: string;
    /** The line's number in the older text; null for an added line. */
    oldNumber: number | null;
    /** The line's number in the newer text; null for a removed line. */
    newNumber: number | null;
}

/** Every line of both texts, in order, with its numbers on each side. */
export function diffRows(from: string, to: string): DiffRow[] {
    const rows: DiffRow[] = [];
    let oldNumber = 1;
    let newNumber = 1;
    for (const part of diffLines(from, to)) {
        const lines = part.value.split("\n");
        if (lines[lines.length - 1] === "") lines.pop();
        for (const text of lines) {
            if (part.added) rows.push({ kind: "added", text, oldNumber: null, newNumber: newNumber++ });
            else if (part.removed) rows.push({ kind: "removed", text, oldNumber: oldNumber++, newNumber: null });
            else rows.push({ kind: "unchanged", text, oldNumber: oldNumber++, newNumber: newNumber++ });
        }
    }
    return rows;
}

/** A stretch of the diff: lines shown, or unchanged lines folded away until the reader opens them. */
export interface DiffBlock {
    kind: "lines" | "folded";
    rows: DiffRow[];
}

/**
 * The hunks: every change with `context` unchanged lines around it. A longer unchanged run folds into one block (a run that
 * would hide a single line stays as it is); texts with no change give no blocks at all.
 */
export function diffBlocks(rows: DiffRow[], context = DIFF_CONTEXT): DiffBlock[] {
    if (rows.every((row) => row.kind === "unchanged")) return [];
    const blocks: DiffBlock[] = [];
    let index = 0;
    while (index < rows.length) {
        const start = index;
        const unchanged = rows[index]?.kind === "unchanged";
        while (index < rows.length && (rows[index]?.kind === "unchanged") === unchanged) index++;
        const run = rows.slice(start, index);
        const keepBefore = start === 0 ? 0 : context;
        const keepAfter = index === rows.length ? 0 : context;
        const hidden = run.length - keepBefore - keepAfter;
        if (!unchanged || hidden < 2) {
            blocks.push({ kind: "lines", rows: run });
            continue;
        }
        if (keepBefore > 0) blocks.push({ kind: "lines", rows: run.slice(0, keepBefore) });
        blocks.push({ kind: "folded", rows: run.slice(keepBefore, run.length - keepAfter) });
        if (keepAfter > 0) blocks.push({ kind: "lines", rows: run.slice(run.length - keepAfter) });
    }
    return blocks;
}
