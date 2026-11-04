import { seriesColor as chartSeriesColor, type ChartChange } from "../../charts/chartStyle";
import { percentValue, seconds } from "../../format/numbers";
import { type RunPassRates } from "./api";

export const ALL_SERIES = "All";

export const CHANGE_MARKS_HELP = "A dashed line marks the run that started a new agent version or new retrieval data.";

export const TREND_MIN_RUNS = 2;

// Why: a tag is never named like these keys, because they start with a dollar sign
export const RUN_KEY = "$run";
export const LABEL_KEY = "$label";
export const DETAILS_KEY = "$details";
export const RECHECK_KEY = "$recheck";

export type TrendRow = Record<string, string | number | null>;

const pad = (value: number) => String(value).padStart(2, "0");

function shortDate(iso: string): string {
    const date = new Date(iso);
    return `${pad(date.getDate())}/${pad(date.getMonth() + 1)} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

interface Reading {
    passRate: number;
    medianMs?: number | null;
    p95Ms?: number | null;
}

export type Measure = (reading: Reading) => number | null;

const passRateMeasure: Measure = (reading) => percentValue(reading.passRate);

export type TurnTimeStat = "median" | "p95";

export function turnTimeMeasure(stat: TurnTimeStat): Measure {
    return (reading) => {
        const milliseconds = stat === "median" ? reading.medianMs : reading.p95Ms;
        return milliseconds == null ? null : seconds(milliseconds);
    };
}

export function withTurnTimes(points: RunPassRates[]): RunPassRates[] {
    return points.filter((point) => point.medianMs != null);
}

export function trendSeries(points: RunPassRates[]): string[] {
    const tags = new Set(points.flatMap((point) => point.tags.map((tag) => tag.tag)));
    return [ALL_SERIES, ...[...tags].sort((a, b) => a.localeCompare(b))];
}

const limitedTo = (point: RunPassRates) => point.limitedToTags ?? [];

export function trendRows(points: RunPassRates[], measure: Measure = passRateMeasure): TrendRow[] {
    const series = trendSeries(points);
    return points.map((point) => {
        const row: TrendRow = {
            [RUN_KEY]: point.runId,
            [LABEL_KEY]: shortDate(point.createdAt),
            [DETAILS_KEY]: [
                limitedTo(point).length === 0 ? "all scenarios" : `tags ${limitedTo(point).join(", ")}`,
                point.versionLabel?.trim() ? point.versionLabel : "no label",
                point.modelNames.join(", "),
            ]
                .filter((part) => part !== "")
                .join(" · "),
            [RECHECK_KEY]: point.recheckedBy ?? null,
        };
        for (const name of series) {
            // Why: a run limited to one tag says nothing about all checks, so All comes only from runs of every scenario
            const reading: Reading | undefined =
                name === ALL_SERIES ? (limitedTo(point).length === 0 ? point : undefined) : point.tags.find((tag) => tag.tag === name);
            row[name] = reading === undefined ? null : measure(reading);
        }
        return row;
    });
}

export function asTrendRow(value: unknown): TrendRow | null {
    return typeof value === "object" && value !== null && RUN_KEY in value ? (value as TrendRow) : null;
}

export function seriesColor(name: string, series: string[]): string {
    return name === ALL_SERIES ? chartSeriesColor(0) : chartSeriesColor(Math.max(series.indexOf(name), 1));
}

export const DEFAULT_TAG_LINES = 10;

export function recentTags(points: readonly RunPassRates[], limit: number): string[] {
    const recent = new Set<string>();
    for (const point of [...points].reverse()) {
        for (const tag of point.tags) {
            if (recent.size < limit) recent.add(tag.tag);
        }
        if (recent.size >= limit) break;
    }
    return [...recent];
}

export function shownSeries(series: readonly string[], picked: readonly string[], recent: readonly string[]): string[] {
    const tags = picked.length === 0 ? recent : picked;
    return series.filter((name) => name === ALL_SERIES || tags.includes(name));
}

export function seriesTags(points: RunPassRates[]): string[] {
    return trendSeries(points).filter((name) => name !== ALL_SERIES);
}

export function hasRechecks(points: readonly RunPassRates[]): boolean {
    return points.some((point) => point.recheckedBy != null);
}

export function measuredSeries(row: TrendRow, series: readonly string[]): string[] {
    return series.filter((name) => typeof row[name] === "number");
}

export function runChanges(points: readonly RunPassRates[], rows: readonly TrendRow[]): ChartChange[] {
    const charted = new Set(rows.map((row) => row[RUN_KEY]));
    const shown = points.filter((point) => charted.has(point.runId));
    const changes: ChartChange[] = [];
    let lastData: string | null = null;
    shown.forEach((point, i) => {
        const previous = shown[i - 1];
        const change: ChartChange = { x: point.runId };
        if (previous && previous.agentVersionId !== point.agentVersionId) {
            change.version = { agentVersionId: point.agentVersionId, label: point.versionLabel };
        }
        const data = point.dataVersion ?? null;
        if (data !== null && lastData !== null && data !== lastData) change.dataVersion = data;
        if (data !== null) lastData = data;
        if (change.version || change.dataVersion !== undefined) changes.push(change);
    });
    return changes;
}
