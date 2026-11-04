import { changeMarks, type ChartMark } from "../../charts/chartStyle";
import type { ChartRow } from "../../charts/LineTrend";
import { percent, percentValue } from "../../format/numbers";

export interface Rate {
    passed: number;
    checked: number;
}

export interface Trend {
    days: { date: string; environment: string; conversations: number; checked: number }[];
    points: { date: string; checkKey: string; passed: number; checked: number }[];
    versions: { at: string; agentVersionId: string; label?: string | null }[];
    dataVersions: { at: string; dataVersion: string }[];
}

export const DAY_KEY = "date";

export const TREND_DAYS = [7, 30, 90] as const;
export type TrendDays = (typeof TREND_DAYS)[number];

export const CONVERSATIONS_SERIES = "Conversations";
const CHECKED_SERIES = "Checked";

export function rateText(rate: Rate): string {
    if (rate.checked === 0) return "—";
    return `${percent(rate.passed / rate.checked)} (${String(rate.passed)}/${String(rate.checked)})`;
}

export function hasFailures(rate: Rate): boolean {
    return rate.passed < rate.checked;
}

function datesOf(trend: Trend): string[] {
    return [...new Set(trend.days.map((day) => day.date))].sort();
}

function dayEnvironments(trend: Trend): string[] {
    return [...new Set(trend.days.map((day) => day.environment))].sort();
}

export function volumeSeries(trend: Trend): string[] {
    const environments = dayEnvironments(trend);
    return [CONVERSATIONS_SERIES, ...(environments.length > 1 ? environments : []), CHECKED_SERIES];
}

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

export function checkKeys(trend: Trend): string[] {
    return [...new Set(trend.points.map((point) => point.checkKey))].sort();
}

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

export function dayMarks(trend: Trend): ChartMark[] {
    const day = (at: string) => at.slice(0, 10);
    return changeMarks(
        [
            ...trend.versions.map((change) => ({ x: day(change.at), version: change })),
            ...trend.dataVersions.map((change) => ({ x: day(change.at), dataVersion: change.dataVersion })),
        ].sort((a, b) => a.x.localeCompare(b.x)),
    );
}

export const MONITORING_CHECKS_PAGE_SIZE = 20;

export const MONITORING_CHECKS_HELP =
    "The checks of suwageval/monitoring-checks.yaml, run on every closed conversation of the deployed agent. The rates count the conversations whose last turn came in the last 7 and 30 days; a failing rate links to its conversations.";
export const MONITORING_TREND_HELP =
    "Per day (UTC, the day of a conversation's last turn): how many conversations came and how many were checked, and each check's pass rate. A dashed line marks the day a new agent version first answered or new retrieval data was first seen.";
