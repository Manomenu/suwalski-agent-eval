import { shortDataVersion } from "../dataVersion/shortDataVersion";
import { versionTitle } from "../format/names";

export const CHART_HEIGHT = 300;

const MAIN_COLOR = "mauve";
const ACCENT_COLORS = ["blue", "green", "yellow", "orange", "teal", "pink", "red"];

export function seriesColor(index: number): string {
    const color = index <= 0 ? MAIN_COLOR : (ACCENT_COLORS[(index - 1) % ACCENT_COLORS.length] ?? "blue");
    return `${color}.5`;
}

export function swatchColor(color: string): string {
    return color.split(".")[0] ?? color;
}

export interface ChartMark {
    x: string;
    label: string;
}

export interface ChartChange {
    x: string;
    version?: { agentVersionId: string; label?: string | null | undefined };
    dataVersion?: string;
}

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

export function markLines(marks: readonly ChartMark[]) {
    return marks.map((mark) => ({
        x: mark.x,
        label: mark.label,
        labelPosition: "insideTopLeft" as const,
        color: "gray.6",
        strokeDasharray: "4 4",
    }));
}

export const percentText = (value: number) => `${String(value)}%`;
