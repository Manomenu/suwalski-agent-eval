import type { ComparedCheck, RateChange, TimeChange, Trend, VariantCell, VariantCheck } from "./api";
import { formatSeconds } from "../../format/numbers";

const BASELINE_PARAM = "baseline";

export function baselineOf(baseline: string): string | null {
    const id = baseline.trim();
    return id === "" ? null : id;
}

export function toggledBaseline(current: string | null, clicked: string): string | null {
    return current === clicked ? null : clicked;
}

export function searchWithBaseline(search: string, baseline: string | null): string {
    const parts = search
        .replace(/^\?/, "")
        .split("&")
        .filter((part) => part !== "" && part.split("=")[0] !== BASELINE_PARAM);
    if (baseline !== null) parts.push(`${BASELINE_PARAM}=${encodeURIComponent(baseline)}`);
    return parts.length === 0 ? "" : `?${parts.join("&")}`;
}

type ValueTrend = Trend | null | undefined;

const trendMarker = (value: ValueTrend): string => (value === "Better" ? "▲" : value === "Worse" ? "▼" : "");

export function trendColor(value: ValueTrend): "green" | "red" | "dimmed" {
    return value === "Better" ? "green" : value === "Worse" ? "red" : "dimmed";
}

export function deltaText(value: RateChange): string {
    if (value.trend === "Same") return "±0 pp";
    return `${trendMarker(value.trend)} ${String(Math.abs(value.points))} pp`;
}

export function timeDeltaText(value: TimeChange): string {
    if (value.trend === "Same") return "±0 s";
    return `${value.milliseconds < 0 ? "▼" : "▲"} ${formatSeconds(Math.abs(value.milliseconds))}`;
}

export const TIME_DELTA_HELP =
    "Change of the turn time against the baseline. Lower is better: a green ▼ is faster, a red ▲ is slower. A change under 5 % or 100 ms counts as the same.";

export type Change = "better" | "worse" | "same";

export const BETTER_BACKGROUND = "var(--mantine-color-green-light)";
export const WORSE_BACKGROUND = "var(--mantine-color-red-light)";

export const CHANGE_TEXT: Record<Change, string> = {
    better: "better than the baseline",
    worse: "worse than the baseline",
    same: "same as the baseline",
};

export function changeBackground(change: Change | null | undefined): string {
    return change === "better" ? BETTER_BACKGROUND : change === "worse" ? WORSE_BACKGROUND : "transparent";
}

export function changeOfTrend(trend: ValueTrend): Change | null {
    return trend === "Better" ? "better" : trend === "Worse" ? "worse" : trend === "Same" ? "same" : null;
}

const SHARE_EPSILON = 1e-9;

export function shareChange(share: number | null | undefined, baseline: number | null | undefined): Change | null {
    if (share == null || baseline == null) return null;
    if (Math.abs(share - baseline) < SHARE_EPSILON) return "same";
    return share > baseline ? "better" : "worse";
}

export function scenarioCellChange(cell: VariantCell | null, baseline: VariantCell | null): Change | null {
    const share = (value: VariantCell | null) =>
        value === null || value.checksTotal === 0 ? null : value.checksPassed / value.checksTotal;
    return shareChange(share(cell), share(baseline));
}

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

export function samplesChange(
    run: { samplesDone: number; samplesFailed?: number },
    baseline: { samplesDone: number; samplesFailed?: number },
): Change | null {
    const share = (value: { samplesDone: number; samplesFailed?: number }) =>
        value.samplesDone === 0 ? null : (value.samplesDone - (value.samplesFailed ?? 0)) / value.samplesDone;
    return shareChange(share(run), share(baseline));
}

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
