// How the line charts of every area look: the same palette, the same marks for a change, the same height.
import { shortDataVersion } from "../dataVersion/shortDataVersion";
import { versionTitle } from "../format/names";

/** The height of a line chart. */
export const CHART_HEIGHT = 300;

/** The main series' colour (the theme's), then the accents in the order the other series come; the accents repeat. */
const MAIN_COLOR = "mauve";
const ACCENT_COLORS = ["blue", "green", "yellow", "orange", "teal", "pink", "red"];

/** The colour of the series at `index` (0: the main one), with its shade, as the chart takes it (`blue.5`). */
export function seriesColor(index: number): string {
    const color = index <= 0 ? MAIN_COLOR : (ACCENT_COLORS[(index - 1) % ACCENT_COLORS.length] ?? "blue");
    return `${color}.5`;
}

/** The colour without its shade (`blue`), for a legend's swatch. */
export function swatchColor(color: string): string {
    return color.split(".")[0] ?? color;
}

/** A point on the x axis where something changed: a new agent version, new retrieval data. */
export interface ChartMark {
    x: string;
    label: string;
}

/** What changed at one point of the x axis: a new agent version, new retrieval data, or both. */
export interface ChartChange {
    x: string;
    version?: { agentVersionId: string; label?: string | null | undefined };
    dataVersion?: string;
}

/** The changes as marks, one per point of the x axis (its changes joined), in the order the points first come. */
export function changeMarks(changes: readonly ChartChange[]): ChartMark[] {
    const labels = new Map<string, string[]>();
    for (const change of changes) {
        const parts = labels.get(change.x) ?? [];
        if (change.version) parts.push(`version ${versionTitle(change.version.agentVersionId, change.version.label)}`);
        if (change.dataVersion !== undefined) parts.push(`data ${shortDataVersion(change.dataVersion)}`);
        labels.set(change.x, parts);
    }
    return [...labels].filter(([, parts]) => parts.length > 0).map(([x, parts]) => ({ x, label: parts.join(", ") }));
}

/** The marks as the chart's dashed reference lines, labelled at their top. */
export function markLines(marks: readonly ChartMark[]) {
    return marks.map((mark) => ({
        x: mark.x,
        label: mark.label,
        labelPosition: "insideTopLeft" as const,
        color: "gray.6",
        strokeDasharray: "4 4",
    }));
}

/** A value with its unit, as the charts' tooltips and axes write it. */
export const percentText = (value: number) => `${String(value)}%`;
