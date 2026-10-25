// The monitoring checks and their trend as the Monitoring page shows them, as plain functions.

/** How a check did over some conversations. */
export interface Rate {
    passed: number;
    checked: number;
}

/** The trend as the server sends it, only what the charts read. */
export interface Trend {
    days: { date: string; conversations: number; checked: number }[];
    points: { date: string; checkKey: string; passed: number; checked: number }[];
    versions: { at: string; agentVersionId: string; label?: string | null }[];
    dataVersions: { at: string; dataVersion: string }[];
}

/** One point of a chart: the day and a number per series. */
export type DayRow = Record<string, string | number | null> & { date: string };

/** A vertical line on the charts: a deploy or new retrieval data, on its day. */
export interface DayMark {
    date: string;
    label: string;
}

/** The days the trend chart can look back. */
export const TREND_DAYS = [7, 30, 90] as const;
export type TrendDays = (typeof TREND_DAYS)[number];

export const CONVERSATIONS_SERIES = "Conversations";
export const CHECKED_SERIES = "Checked";

/** "80% (4/5)", or "—" when the check ran on no conversation. */
export function rateText(rate: Rate): string {
    if (rate.checked === 0) return "—";
    return `${String(Math.round((100 * rate.passed) / rate.checked))}% (${String(rate.passed)}/${String(rate.checked)})`;
}

/** A rate that is not all passed: worth a colour and a link to its failing conversations. */
export function hasFailures(rate: Rate): boolean {
    return rate.passed < rate.checked;
}

/** Per day: the conversations and how many of them were checked. */
export function volumeRows(trend: Trend): DayRow[] {
    return trend.days.map((day) => ({ date: day.date, [CONVERSATIONS_SERIES]: day.conversations, [CHECKED_SERIES]: day.checked }));
}

/** The checks the trend has verdicts of, in key order. */
export function checkKeys(trend: Trend): string[] {
    return [...new Set(trend.points.map((point) => point.checkKey))].sort();
}

/** Per day and check: the pass rate in percent over every version that answered that day; null on a day the check did not run. */
export function passRateRows(trend: Trend): DayRow[] {
    const keys = checkKeys(trend);
    return trend.days.map((day) => {
        const row: DayRow = { date: day.date };
        for (const key of keys) {
            const points = trend.points.filter((point) => point.date === day.date && point.checkKey === key);
            const checked = points.reduce((sum, point) => sum + point.checked, 0);
            const passed = points.reduce((sum, point) => sum + point.passed, 0);
            row[key] = checked === 0 ? null : Math.round((1000 * passed) / checked) / 10;
        }
        return row;
    });
}

/** The deploys and the data changes as marks on the charts, on the day (UTC) they were first seen; marks of one day are joined. */
export function dayMarks(trend: Trend): DayMark[] {
    const labels = new Map<string, string[]>();
    const add = (at: string, label: string) => {
        const date = at.slice(0, 10);
        labels.set(date, [...(labels.get(date) ?? []), label]);
    };
    for (const change of trend.versions) {
        add(change.at, `version ${change.label?.trim() ? change.label : change.agentVersionId.slice(0, 8)}`);
    }
    for (const change of trend.dataVersions) add(change.at, `data ${change.dataVersion}`);
    return [...labels].sort(([a], [b]) => a.localeCompare(b)).map(([date, parts]) => ({ date, label: parts.join(", ") }));
}

export const MONITORING_CHECKS_HELP =
    "The checks of suwageval/monitoring-checks.yaml, run on every closed conversation of the deployed agent. The rates count the conversations whose last turn came in the last 7 and 30 days; a failing rate links to its conversations.";
export const MONITORING_TREND_HELP =
    "Per day (UTC, the day of a conversation's last turn): how many conversations came and how many were checked, and each check's pass rate. A dashed line marks the day a new agent version first answered or new retrieval data was first seen.";
