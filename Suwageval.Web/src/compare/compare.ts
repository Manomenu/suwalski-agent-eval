// What the comparison page shows of what the server computed (`GET /runs/compare`): the run ids of the address, and the colours, markers
// and texts of cells, changes and headers. Which checks line up, what differs, every rate, trend and change is the server's.
import { COMPARE_MAX_RUNS, COMPARE_MIN_RUNS } from "../agents/view";
import { formatSeconds, percent } from "../runs/results";
import type { ComparedCell, ComparedCheck, RateChange, TimeChange, Trend } from "./api";

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export type RunIds = { kind: "ok"; ids: string[] } | { kind: "error"; message: string };

/** The runs of `?runs=a,b,c`, each once, in the order given; or what is wrong with them. */
export function parseRunIds(raw: string): RunIds {
    const ids = [
        ...new Set(
            raw
                .split(",")
                .map((id) => id.trim())
                .filter((id) => id !== ""),
        ),
    ];
    const bad = ids.find((id) => !GUID.test(id));
    if (bad !== undefined) return { kind: "error", message: `"${bad}" is not a run id.` };
    if (ids.length < COMPARE_MIN_RUNS || ids.length > COMPARE_MAX_RUNS) {
        return { kind: "error", message: `Choose ${COMPARE_MIN_RUNS} to ${COMPARE_MAX_RUNS} runs to compare.` };
    }
    return { kind: "ok", ids };
}

/** The run ids with the baseline first, the others in the order they were: the server reads the first run as the baseline. */
export function baselineFirst(ids: readonly string[], baselineId: string): string[] {
    return [baselineId, ...ids.filter((id) => id !== baselineId)];
}

/** The checks of one run among these rows, counted as the server counts a run's: a cell is a check; a run without it adds nothing. */
export function columnChecksCounts(rows: readonly ComparedCheck[], column: number): { checksPassed: number; checksFailed: number } {
    const cells = rows.flatMap((row) => row.cells[column] ?? []);
    const checksPassed = cells.filter((cell) => cell.passed).length;
    return { checksPassed, checksFailed: cells.length - checksPassed };
}

/** The change of a cell against the baseline's: none when either run has no check to compare. */
export type CellTrend = Trend | null | undefined;

export const trendMarker = (value: CellTrend): string => (value === "Better" ? "▲" : value === "Worse" ? "▼" : "");

/** Mantine colour of a trend: green better, red worse, nothing otherwise. */
export function trendColor(value: CellTrend): "green" | "red" | "dimmed" {
    return value === "Better" ? "green" : value === "Worse" ? "red" : "dimmed";
}

export interface CellTone {
    /** The background: green passed, red failed, yellow failed with samples nobody could judge, gray not in that run. */
    color: "green" | "red" | "yellow" | "gray";
    /** The cell differs from the baseline's for the better or the worse: it is drawn with a frame. */
    emphasised: boolean;
}

export function cellTone(cell: ComparedCell | null, cellTrend: CellTrend): CellTone {
    if (cell === null) return { color: "gray", emphasised: false };
    const color = cell.passed ? "green" : cell.notJudged > 0 ? "yellow" : "red";
    return { color, emphasised: cellTrend === "Better" || cellTrend === "Worse" };
}

/** `60%` with its samples, `3/5`; a dash for a run without the check. */
export function cellText(cell: ComparedCell | null): { rate: string; samples: string } {
    if (cell === null) return { rate: "—", samples: "" };
    const notJudged = cell.notJudged > 0 ? ` · ${String(cell.notJudged)} not judged` : "";
    return { rate: percent(cell.passRate), samples: `${String(cell.samplesPassed)}/${String(cell.samples)}${notJudged}` };
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
