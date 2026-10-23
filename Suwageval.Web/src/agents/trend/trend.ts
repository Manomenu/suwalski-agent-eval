import { percentValue, seconds } from "../../format/numbers";
import { versionTitle } from "../../format/names";
import { type RunPassRates } from "./api";

/** The series of every run's rate over all checks. */
export const ALL_SERIES = "All";

/** The trend needs this many points to be a line. */
export const TREND_MIN_RUNS = 2;

/** Keys of a chart row beside the series: a tag is never named like these (they start with `$`). */
export const RUN_KEY = "$run";
export const LABEL_KEY = "$label";
export const DETAILS_KEY = "$details";
/** The run whose re-check the point shows; null when it shows the run's own results. */
export const RECHECK_KEY = "$recheck";

/** A point of the chart: the run, its short date, its version and model, and a percentage per series (`null`: no check with that tag). */
export type TrendRow = Record<string, string | number | null>;

const pad = (value: number) => String(value).padStart(2, "0");

/** `16/10 14:05`, in the browser's time zone. */
function shortDate(iso: string): string {
    const date = new Date(iso);
    return `${pad(date.getDate())}/${pad(date.getMonth() + 1)} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** What a chart reads from a point or from one of its tags (`RunPassRates` and `TagPassRate` both have these). */
interface Reading {
    passRate: number;
    medianMs?: number | null;
    p95Ms?: number | null;
}

/** The number a chart plots from a reading, in the chart's own unit; null when the reading has none. */
export type Measure = (reading: Reading) => number | null;

const passRateMeasure: Measure = (reading) => percentValue(reading.passRate);

/** Which turn time a chart shows. */
export type TurnTimeStat = "median" | "p95";

export function turnTimeMeasure(stat: TurnTimeStat): Measure {
    return (reading) => {
        const milliseconds = stat === "median" ? reading.medianMs : reading.p95Ms;
        return milliseconds == null ? null : seconds(milliseconds);
    };
}

/** The runs that made a call: only they have a turn time. */
export function withTurnTimes(points: RunPassRates[]): RunPassRates[] {
    return points.filter((point) => point.medianMs != null);
}

/** "All" first, then every tag any run has, sorted. */
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
            // A run limited to one tag says nothing about all checks: "All" comes only from runs of every scenario.
            const reading: Reading | undefined =
                name === ALL_SERIES ? (limitedTo(point).length === 0 ? point : undefined) : point.tags.find((tag) => tag.tag === name);
            row[name] = reading === undefined ? null : measure(reading);
        }
        return row;
    });
}

/** The row behind a tooltip's payload, or null when it is not one of ours. */
export function asTrendRow(value: unknown): TrendRow | null {
    return typeof value === "object" && value !== null && RUN_KEY in value ? (value as TrendRow) : null;
}

/** Mocha accents after mauve ("All"), in the order the series come; the colours repeat when there are more tags. */
const TAG_COLORS = ["blue", "green", "yellow", "orange", "teal", "pink", "red"];

/** The colour without its shade (`yellow`), for the legend's swatches. */
export function legendColor(name: string, series: string[]): string {
    return seriesColor(name, series).split(".")[0] ?? "blue";
}

export function seriesColor(name: string, series: string[]): string {
    if (name === ALL_SERIES) return "mauve.5";
    const index = Math.max(series.indexOf(name) - 1, 0);
    return `${TAG_COLORS[index % TAG_COLORS.length] ?? "blue"}.5`;
}

/** The set of hidden series after a click on `name`. */
export function toggleHidden(hidden: ReadonlySet<string>, name: string): ReadonlySet<string> {
    const next = new Set(hidden);
    if (!next.delete(name)) next.add(name);
    return next;
}

/** Some point shows the results of a re-check, not of its own run: the chart says so. */
export function hasRechecks(points: readonly RunPassRates[]): boolean {
    return points.some((point) => point.recheckedBy != null);
}

/** The series a run measured, in the legend's order: a line passing over a run that did not measure it shows no value there. */
export function measuredSeries(row: TrendRow, series: readonly string[]): string[] {
    return series.filter((name) => typeof row[name] === "number");
}

/** A run of the chart whose agent version differs from the run before it: where a line marks the change. */
export interface VersionChange {
    runId: string;
    label: string;
}

/** The runs of `rows` (oldest first) that start a new agent version, with the version's name; none for the first run. */
export function versionChanges(points: readonly RunPassRates[], rows: readonly TrendRow[]): VersionChange[] {
    const charted = new Set(rows.map((row) => row[RUN_KEY]));
    const shown = points.filter((point) => charted.has(point.runId));
    return shown
        .filter((point, i) => i > 0 && shown[i - 1]?.agentVersionId !== point.agentVersionId)
        .map((point) => ({ runId: point.runId, label: versionTitle(point.agentVersionId, point.versionLabel) }));
}
