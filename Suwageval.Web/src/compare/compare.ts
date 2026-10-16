// The rules of comparing runs, as plain functions: which runs may be compared, how their checks line up, the pass rate per
// tag, what counts as a difference and how a cell is coloured. The components only render what these return.
//
// Words: a CHECK is "passed" when its pass rate reaches its threshold (the server says so in `passed`); its PASS RATE is the
// share of its samples that passed. A run's pass rate over some scenarios is the share of their checks that are passed — a
// check counts once, however many samples it has.
import { COMPARE_MAX_RUNS, COMPARE_MIN_RUNS } from "../agents/view";
import type { CheckRunResult, RunSummary, ScenarioRunResult } from "../runs/api";
import { notJudgedCount, num, percent } from "../runs/results";

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

export const DIFFERENT_AGENTS = "Runs of different agents cannot be compared";

/** True when every run is of the same agent. */
export function sameAgent(runs: { run: { agent: string } }[]): boolean {
    return new Set(runs.map((results) => results.run.agent)).size <= 1;
}

/** What a run's scenarios say about one check. */
export interface CheckCell {
    /** Passed its threshold. */
    passed: boolean;
    /** 0 to 1. */
    passRate: number;
    /** Samples that passed, and all of them. */
    passes: number;
    samples: number;
    /** Samples the judge could not judge (its model down, an unreadable answer): counted as failed, but not a verdict. */
    notJudged: number;
}

export function cellOf(check: CheckRunResult): CheckCell {
    return {
        passed: check.passed,
        passRate: num(check.passRate),
        passes: check.samples.filter((sample) => sample.passed).length,
        samples: check.samples.length,
        notJudged: notJudgedCount(check),
    };
}

/** One check across the runs: a cell per run, `null` for a run that has no such check (or no such scenario). */
export interface CheckRow {
    scenarioKey: string;
    checkKey: string;
    cells: (CheckCell | null)[];
}

interface RunScenarios {
    scenarios: ScenarioRunResult[];
}

/** The checks of all runs lined up by scenario key and check key, in the order they first appear (the baseline's first). */
export function alignRows(runs: RunScenarios[]): CheckRow[] {
    const rows = new Map<string, CheckRow>();
    runs.forEach((results, column) => {
        for (const scenario of results.scenarios) {
            for (const check of scenario.checks) {
                const id = JSON.stringify([scenario.key, check.key]);
                let row = rows.get(id);
                if (row === undefined) {
                    row = { scenarioKey: scenario.key, checkKey: check.key, cells: runs.map(() => null) };
                    rows.set(id, row);
                }
                row.cells[column] = cellOf(check);
            }
        }
    });
    return [...rows.values()];
}

function sameCell(a: CheckCell | null, b: CheckCell | null): boolean {
    if (a === null || b === null) return a === b;
    return a.passed === b.passed && a.passRate === b.passRate;
}

/** The runs differ on this check: a cell differs from another, and a check missing from some runs is a difference too. */
export function rowDiffers(row: CheckRow): boolean {
    return row.cells.some((cell) => !sameCell(cell, row.cells[0] ?? null));
}

/** Every run has this check, so the runs can be compared on it. */
export function rowComparable(row: CheckRow): boolean {
    return row.cells.every((cell) => cell !== null);
}

export interface Switches {
    onlyDifferences: boolean;
    hideUncomparable: boolean;
}

/** The rows the two switches leave; they combine. */
export function visibleRows(rows: CheckRow[], switches: Switches): CheckRow[] {
    return rows.filter((row) => (!switches.onlyDifferences || rowDiffers(row)) && (!switches.hideUncomparable || rowComparable(row)));
}

/** How a cell stands against the baseline's: better, worse, the same — or `none` when either has no check to compare. */
export type Trend = "better" | "worse" | "same" | "none";

export function trend(cell: CheckCell | null, baseline: CheckCell | null): Trend {
    if (cell === null || baseline === null) return "none";
    if (cell.passed !== baseline.passed) return cell.passed ? "better" : "worse";
    if (cell.passRate === baseline.passRate) return "same";
    return cell.passRate > baseline.passRate ? "better" : "worse";
}

export const trendMarker = (value: Trend): string => (value === "better" ? "▲" : value === "worse" ? "▼" : "");

/** Mantine colour of a trend: green better, red worse, nothing otherwise. */
export function trendColor(value: Trend): "green" | "red" | "dimmed" {
    return value === "better" ? "green" : value === "worse" ? "red" : "dimmed";
}

export interface CellTone {
    /** The background: green passed, red failed, yellow failed with samples nobody could judge, gray not in that run. */
    color: "green" | "red" | "yellow" | "gray";
    /** The cell differs from the baseline's for the better or the worse: it is drawn with a frame. */
    emphasised: boolean;
}

export function cellTone(cell: CheckCell | null, cellTrend: Trend): CellTone {
    if (cell === null) return { color: "gray", emphasised: false };
    const color = cell.passed ? "green" : cell.notJudged > 0 ? "yellow" : "red";
    return { color, emphasised: cellTrend === "better" || cellTrend === "worse" };
}

/** `60%` with its samples, `3/5`; a dash for a run without the check. */
export function cellText(cell: CheckCell | null): { rate: string; samples: string } {
    if (cell === null) return { rate: "—", samples: "" };
    const notJudged = cell.notJudged > 0 ? ` · ${String(cell.notJudged)} not judged` : "";
    return { rate: percent(cell.passRate), samples: `${String(cell.passes)}/${String(cell.samples)}${notJudged}` };
}

/** The share of the checks of these scenarios that are passed, 0 to 1; `null` when they have no checks. */
export function passRateOf(scenarios: ScenarioRunResult[]): number | null {
    const checks = scenarios.flatMap((scenario) => scenario.checks);
    if (checks.length === 0) return null;
    return checks.filter((check) => check.passed).length / checks.length;
}

/** A row of the per-tag summary: `tag` is null for the "All" row; a rate per run, `null` where the run has no check with that tag. */
export interface TagRow {
    tag: string | null;
    rates: (number | null)[];
}

/**
 * "All" (every check of the run), then one row per tag, sorted by name. The tags are the scenarios' current ones (`tagsOf`: scenario
 * key to tags); a scenario the agent no longer has counts in "All" only.
 */
export function tagRows(runs: RunScenarios[], tagsOf: ReadonlyMap<string, readonly string[]>): TagRow[] {
    const tags = [...new Set(runs.flatMap((results) => results.scenarios.flatMap((scenario) => tagsOf.get(scenario.key) ?? [])))].sort(
        (a, b) => a.localeCompare(b),
    );
    return [
        { tag: null, rates: runs.map((results) => passRateOf(results.scenarios)) },
        ...tags.map((tag) => ({
            tag,
            rates: runs.map((results) =>
                passRateOf(results.scenarios.filter((scenario) => tagsOf.get(scenario.key)?.includes(tag) === true)),
            ),
        })),
    ];
}

/** A rate against the baseline's, in percentage points rounded to one decimal; `null` when either run has no such checks. */
export interface Delta {
    points: number;
    trend: "better" | "worse" | "same";
}

export function delta(rate: number | null, baseline: number | null): Delta | null {
    if (rate === null || baseline === null) return null;
    const points = Math.round((rate - baseline) * 1000) / 10;
    return { points, trend: points > 0 ? "better" : points < 0 ? "worse" : "same" };
}

/** `▲ 12.5 pp`, `▼ 3 pp`, `±0 pp`. */
export function deltaText(value: Delta): string {
    if (value.trend === "same") return "±0 pp";
    return `${trendMarker(value.trend)} ${Math.abs(value.points)} pp`;
}

/** The "N/M checks passed" of a run's header, over every check so far. */
export function checksPassedText(run: Pick<RunSummary, "checksPassed" | "checksFailed">): string {
    const passed = num(run.checksPassed);
    return `${passed}/${passed + num(run.checksFailed)} checks passed`;
}
