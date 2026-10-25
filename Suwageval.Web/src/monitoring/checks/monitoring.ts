// The monitoring checks and their trend as the Monitoring page shows them, as plain functions.
import { changeMarks, type ChartMark } from "../../charts/chartStyle";
import type { ChartRow } from "../../charts/LineTrend";
import { percent, percentValue } from "../../format/numbers";

/** How a check did over some conversations. */
export interface Rate {
    passed: number;
    checked: number;
}

/** The trend as the server sends it, only what the charts read. */
export interface Trend {
    days: { date: string; environment: string; conversations: number; checked: number }[];
    points: { date: string; checkKey: string; passed: number; checked: number }[];
    versions: { at: string; agentVersionId: string; label?: string | null }[];
    dataVersions: { at: string; dataVersion: string }[];
}

/** The key of a chart row's day (UTC, `yyyy-MM-dd`). */
export const DAY_KEY = "date";

/** The days the trend chart can look back. */
export const TREND_DAYS = [7, 30, 90] as const;
export type TrendDays = (typeof TREND_DAYS)[number];

export const CONVERSATIONS_SERIES = "Conversations";
const CHECKED_SERIES = "Checked";

/** "80% (4/5)", or "—" when the check ran on no conversation. */
export function rateText(rate: Rate): string {
    if (rate.checked === 0) return "—";
    return `${percent(rate.passed / rate.checked)} (${String(rate.passed)}/${String(rate.checked)})`;
}

/** A rate that is not all passed: worth a colour and a link to its failing conversations. */
export function hasFailures(rate: Rate): boolean {
    return rate.passed < rate.checked;
}

/** The days of the trend, oldest first, each once (a day has a row per environment). */
function datesOf(trend: Trend): string[] {
    return [...new Set(trend.days.map((day) => day.date))].sort();
}

/** The environments the trend's conversations came from, sorted: a line each when there is more than one. */
function dayEnvironments(trend: Trend): string[] {
    return [...new Set(trend.days.map((day) => day.environment))].sort();
}

/** The series of the conversations chart: all of them, then one per environment when there are several, then how many were checked. */
export function volumeSeries(trend: Trend): string[] {
    const environments = dayEnvironments(trend);
    return [CONVERSATIONS_SERIES, ...(environments.length > 1 ? environments : []), CHECKED_SERIES];
}

/** Per day: the conversations in all, in each environment, and how many of them were checked. */
export function volumeRows(trend: Trend): ChartRow[] {
    return datesOf(trend).map((date) => {
        const days = trend.days.filter((day) => day.date === date);
        const row: ChartRow = {
            [DAY_KEY]: date,
            [CONVERSATIONS_SERIES]: days.reduce((sum, day) => sum + day.conversations, 0),
            [CHECKED_SERIES]: days.reduce((sum, day) => sum + day.checked, 0),
        };
        for (const day of days) row[day.environment] = day.conversations;
        return row;
    });
}

/** The checks the trend has verdicts of, in key order. */
export function checkKeys(trend: Trend): string[] {
    return [...new Set(trend.points.map((point) => point.checkKey))].sort();
}

/** Per day and check: the pass rate in percent over every version that answered that day; null on a day the check did not run. */
export function passRateRows(trend: Trend): ChartRow[] {
    const keys = checkKeys(trend);
    return datesOf(trend).map((date) => {
        const row: ChartRow = { [DAY_KEY]: date };
        for (const key of keys) {
            const points = trend.points.filter((point) => point.date === date && point.checkKey === key);
            const checked = points.reduce((sum, point) => sum + point.checked, 0);
            const passed = points.reduce((sum, point) => sum + point.passed, 0);
            row[key] = checked === 0 ? null : percentValue(passed / checked);
        }
        return row;
    });
}

/** The deploys and the data changes as marks on the charts, on the day (UTC) they were first seen; marks of one day are joined. */
export function dayMarks(trend: Trend): ChartMark[] {
    const day = (at: string) => at.slice(0, 10);
    return changeMarks(
        [
            ...trend.versions.map((change) => ({ x: day(change.at), version: change })),
            ...trend.dataVersions.map((change) => ({ x: day(change.at), dataVersion: change.dataVersion })),
        ].sort((a, b) => a.x.localeCompare(b.x)),
    );
}

/** How many monitoring checks the table shows a page. */
export const MONITORING_CHECKS_PAGE_SIZE = 20;

export const MONITORING_CHECKS_HELP =
    "The checks of suwageval/monitoring-checks.yaml, run on every closed conversation of the deployed agent. The rates count the conversations whose last turn came in the last 7 and 30 days; a failing rate links to its conversations.";
export const MONITORING_TREND_HELP =
    "Per day (UTC, the day of a conversation's last turn): how many conversations came and how many were checked, and each check's pass rate. A dashed line marks the day a new agent version first answered or new retrieval data was first seen.";
