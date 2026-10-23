// What the comparison page shows of the baseline-relative numbers the server computed (`GET /runs/compare?baseline=`): the baseline in
// the address, and the markers, colours and texts of changes. Every rate, trend and change is the server's.
import type { ComparedCheck, RateChange, TimeChange, Trend, VariantCell, VariantCheck } from "./api";
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

/** How a cell came out against the baseline's cell of the same row. */
export type Change = "better" | "worse" | "same";

/** Light backgrounds (the text stays readable) of a cell that is better or worse than the baseline's; the same in every row. */
export const BETTER_BACKGROUND = "var(--mantine-color-green-light)";
export const WORSE_BACKGROUND = "var(--mantine-color-red-light)";

/** What a screen reader hears in place of the colour. */
export const CHANGE_TEXT: Record<Change, string> = {
    better: "better than the baseline",
    worse: "worse than the baseline",
    same: "same as the baseline",
};

/** The background of a cell for its change; nothing (neutral) for the same or for no change. */
export function changeBackground(change: Change | null | undefined): string {
    return change === "better" ? BETTER_BACKGROUND : change === "worse" ? WORSE_BACKGROUND : "transparent";
}

/** The server's trend as a change; none when it has no trend. */
export function changeOfTrend(trend: ValueTrend): Change | null {
    return trend === "Better" ? "better" : trend === "Worse" ? "worse" : trend === "Same" ? "same" : null;
}

const SHARE_EPSILON = 1e-9;

/** A share (higher is better) against the baseline's: none when either is missing. */
export function shareChange(share: number | null | undefined, baseline: number | null | undefined): Change | null {
    if (share == null || baseline == null) return null;
    if (Math.abs(share - baseline) < SHARE_EPSILON) return "same";
    return share > baseline ? "better" : "worse";
}

/** A scenario's cell against the baseline's cell of the same scenario, by the share of its checks that passed; none when either is missing or has no check. */
export function scenarioCellChange(cell: VariantCell | null, baseline: VariantCell | null): Change | null {
    const share = (value: VariantCell | null) =>
        value === null || value.checksTotal === 0 ? null : value.checksPassed / value.checksTotal;
    return shareChange(share(cell), share(baseline));
}

/** A check's cell in the opened scenario: the server's trend when it has one, else the pass rates compared. */
export function checkCellChange(
    check: VariantCheck | null,
    baselineCheck: VariantCheck | null,
    compared: ComparedCheck | undefined,
    column: number,
): Change | null {
    if (check === null) return null;
    const trend = changeOfTrend(compared?.cells[column]?.trend);
    return trend ?? shareChange(check.passRate, baselineCheck?.passRate);
}

/** The Summary's checks row: the share of checks passed against the baseline's. */
export function checksChange(
    run: { checksPassed: number; checksFailed: number },
    baseline: { checksPassed: number; checksFailed: number },
): Change | null {
    const share = (value: { checksPassed: number; checksFailed: number }) => {
        const total = value.checksPassed + value.checksFailed;
        return total === 0 ? null : value.checksPassed / total;
    };
    return shareChange(share(run), share(baseline));
}

/** The Summary's samples row: the share of samples the agent answered against the baseline's. */
export function samplesChange(
    run: { samplesDone: number; samplesFailed?: number },
    baseline: { samplesDone: number; samplesFailed?: number },
): Change | null {
    const share = (value: { samplesDone: number; samplesFailed?: number }) =>
        value.samplesDone === 0 ? null : (value.samplesDone - (value.samplesFailed ?? 0)) / value.samplesDone;
    return shareChange(share(run), share(baseline));
}

/** The column "What differs" reads the others against: the baseline's when one is picked, else the first. */
export function referenceColumn({
    runs,
    baseline,
}: {
    runs: readonly { run: { id: string } }[];
    baseline?: { runId: string } | null;
}): number {
    const index = baseline == null ? -1 : runs.findIndex((column) => column.run.id === baseline.runId);
    return Math.max(index, 0);
}
