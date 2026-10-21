// What the comparison page shows of the baseline-relative numbers the server computed (`GET /runs/compare`): the baseline order of the
// runs, and the markers, colours and texts of changes. Every rate, trend and change is the server's.
import { formatSeconds } from "../runs/results";
import type { RateChange, TimeChange, Trend } from "./api";

/** The run ids with the baseline first, the others in the order they were: the server reads the first run as the baseline. */
export function baselineFirst(ids: readonly string[], baselineId: string): string[] {
    return [baselineId, ...ids.filter((id) => id !== baselineId)];
}

/** The change of a value against the baseline's: none when either run has nothing to compare. */
type ValueTrend = Trend | null | undefined;

const trendMarker = (value: ValueTrend): string => (value === "Better" ? "▲" : value === "Worse" ? "▼" : "");

/** Mantine colour of a trend: green better, red worse, nothing otherwise. */
export function trendColor(value: ValueTrend): "green" | "red" | "dimmed" {
    return value === "Better" ? "green" : value === "Worse" ? "red" : "dimmed";
}

/** `▲ 12.5 pp`, `▼ 3 pp`, `±0 pp`. */
export function deltaText(value: RateChange): string {
    if (value.trend === "Same") return "±0 pp";
    return `${trendMarker(value.trend)} ${String(Math.abs(value.points))} pp`;
}

/** `▼ 1.2 s` for a faster run, `▲ 3.0 s` for a slower one, `±0 s` for the same: the arrow follows the time, not the verdict. */
export function timeDeltaText(value: TimeChange): string {
    if (value.trend === "Same") return "±0 s";
    return `${value.milliseconds < 0 ? "▼" : "▲"} ${formatSeconds(Math.abs(value.milliseconds))}`;
}

/** Said on hover over a turn time change; the thresholds are the server's (`RunComparer.TimeSameShare`, `TimeSameMs`). */
export const TIME_DELTA_HELP =
    "Change of the turn time against the baseline. Lower is better: a green ▼ is faster, a red ▲ is slower. A change under 5 % or 100 ms counts as the same.";
