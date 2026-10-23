// What the comparison page shows of the baseline-relative numbers the server computed (`GET /runs/compare?baseline=`): the baseline in
// the address, and the markers, colours and texts of changes. Every rate, trend and change is the server's.
import type { RateChange, TimeChange, Trend } from "./api";
import { formatSeconds } from "../format/numbers";

const BASELINE_PARAM = "baseline";

/** The baseline the address asks for (`?baseline=<id>`); null when it asks for none. */
export function baselineOf(baseline: string): string | null {
    const id = baseline.trim();
    return id === "" ? null : id;
}

/** The baseline after a click on a column's button: that run, or none when it already is the baseline (back to the absolute view). */
export function toggledBaseline(current: string | null, clicked: string): string | null {
    return current === clicked ? null : clicked;
}

/** The query with its baseline parameter set to `baseline` (removed when null); every other parameter stays exactly as it was written. */
export function searchWithBaseline(search: string, baseline: string | null): string {
    const parts = search
        .replace(/^\?/, "")
        .split("&")
        .filter((part) => part !== "" && part.split("=")[0] !== BASELINE_PARAM);
    if (baseline !== null) parts.push(`${BASELINE_PARAM}=${encodeURIComponent(baseline)}`);
    return parts.length === 0 ? "" : `?${parts.join("&")}`;
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
