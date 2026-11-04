import { shortId } from "../../format/names";

export function printMaxRuns(pageWidth: number, labelWidth: number, columnWidth: number): number {
    return Math.max(0, Math.floor((pageWidth - labelWidth) / columnWidth));
}

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
