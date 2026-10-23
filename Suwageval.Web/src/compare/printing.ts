// Printing the comparison: how many run columns fit a page, and the line at the top of the paper (the filters are not printed).
import { shortId } from "../format/names";

/** The run columns that fit a page of `pageWidth` after the label column, each at least `columnWidth` wide. */
export function printMaxRuns(pageWidth: number, labelWidth: number, columnWidth: number): number {
    return Math.max(0, Math.floor((pageWidth - labelWidth) / columnWidth));
}

/** Why the Print button is off, or undefined when `runs` fit a printed page. */
export function printDisabledReason(runs: number, max: number): string | undefined {
    return runs <= max
        ? undefined
        : `At most ${String(max)} runs fit a printed page — compare fewer runs, e.g. with the same baseline, and print each.`;
}

/** "Comparison of support · group sweep · runs 1a2b3c4d, 5e6f7a8b · baseline 1a2b3c4d · scenarios with tags: smoke · only rows that differ". */
export function comparePrintLine(view: {
    agent: string;
    group: string | null | undefined;
    runIds: readonly string[];
    baselineId: string | null | undefined;
    selectedTags: readonly string[];
    onlyDifferences: boolean;
    byTag: boolean;
}): string {
    const parts = [`Comparison of ${view.agent}`];
    if (view.group != null) parts.push(`group ${view.group}`);
    parts.push(`runs ${view.runIds.map(shortId).join(", ")}`);
    parts.push(view.baselineId == null ? "no baseline" : `baseline ${shortId(view.baselineId)}`);
    parts.push(view.selectedTags.length === 0 ? "all scenarios" : `scenarios with tags: ${view.selectedTags.join(", ")}`);
    if (view.onlyDifferences) parts.push("only rows that differ");
    if (view.byTag) parts.push("listed by tag");
    return parts.join(" · ");
}
