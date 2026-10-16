// What a check's state against its history means for the pages (the server decides the state): labels, colours, the order the run
// page lists them in and the links to follow.
import type { CheckRegression, CheckRunResult, RegressionState, RegressionSummary, RunResults } from "./api";
import { runHref, shortId } from "./results";

export function stateLabel(state: RegressionState): string {
    switch (state) {
        case "Passed":
            return "Passed";
        case "Regression":
            return "Regression";
        case "New":
            return "New";
        case "AlreadyBroken":
            return "Already broken";
        case "KnownFailure":
            return "Known failure";
        case "Fixed":
            return "Fixed";
        case "Unstable":
            return "Unstable";
    }
}

/** A Mantine theme colour of the state. */
export function stateColor(state: RegressionState): string {
    switch (state) {
        case "Passed":
            return "green";
        case "Regression":
            return "red";
        case "New":
            return "blue";
        case "AlreadyBroken":
            return "orange";
        case "KnownFailure":
            return "gray";
        case "Fixed":
            return "green";
        case "Unstable":
            return "yellow";
    }
}

/** "Compared with master" / "Compared with every run of the agent" (a run without git). */
export function comparedWithHeading(summary: RegressionSummary): string {
    return summary.comparedWith == null ? "Compared with every run of the agent" : `Compared with ${summary.comparedWith}`;
}

/** The history in words: "last 5 reference runs" (or "last 5 runs" without git). */
export function historyText(summary: RegressionSummary): string {
    const runs = summary.comparedWith == null ? "runs" : "reference runs";
    return `last ${summary.history} ${runs}`;
}

/** The compare page of the last run the check passed in and this one, or null when it never passed in the history. */
export function regressionCompareHref(runId: string, lastPassedRun: string | null | undefined): string | null {
    return lastPassedRun == null ? null : `/compare?runs=${lastPassedRun},${runId}`;
}

/**
 * The link under a check of a run: Fixed compares with the last run the check failed in, the other states with the last run it
 * passed in (an already broken check has none: it was not this run that broke it). Null when there is no such run.
 */
export function compareLink(runId: string, regression: CheckRegression | null | undefined): { label: string; href: string } | null {
    if (regression == null || regression.state === "AlreadyBroken") return null;
    if (regression.state === "Fixed") {
        const failed = [...(regression.history ?? [])].reverse().find((point) => !point.passed);
        return failed === undefined ? null : { label: "Compare with the last failing run", href: `/compare?runs=${failed.runId},${runId}` };
    }
    const href = regressionCompareHref(runId, regression.lastPassedRun);
    return href === null ? null : { label: "Compare with the last passing run", href };
}

/** "17/10 00:03" in the viewer's time zone. */
export function historyTime(iso: string): string {
    const time = new Date(iso);
    const two = (value: number) => String(value).padStart(2, "0");
    return `${two(time.getDate())}/${two(time.getMonth() + 1)} ${two(time.getHours())}:${two(time.getMinutes())}`;
}

/** What the strip of marks is and how to read it, for someone who meets it for the first time. */
export const CHECK_HISTORY_EXPLANATION =
    "This check's verdict in the newest reference runs it is compared with, oldest first; the last mark is this run. Its state (regression, already broken, fixed, unstable…) follows from this pattern. Click a mark to open that run.";

/** One dot of a check's history strip. A mark without `href` is the run being shown. */
export interface HistoryMark {
    key: string;
    passed: boolean;
    /** What a screen reader says: "run 725c66c4: passed" / "this run: failed". */
    label: string;
    /** The tooltip's first line: "17/10 00:03 · run 725c66c4 · passed" / "this run · failed". */
    tooltip: string;
    href: string | null;
}

/** The marks of a check's history oldest first and then this run's; none when the check has no history. */
export function historyMarks(check: Pick<CheckRunResult, "passed" | "regression">): HistoryMark[] {
    const history = check.regression?.history ?? [];
    if (history.length === 0) return [];
    const verdict = (passed: boolean) => (passed ? "passed" : "failed");
    return [
        ...history.map((point) => ({
            key: point.runId,
            passed: point.passed,
            label: `run ${shortId(point.runId)}: ${verdict(point.passed)}`,
            tooltip: `${historyTime(point.createdAt)} · run ${shortId(point.runId)} · ${verdict(point.passed)}`,
            href: runHref(point.runId),
        })),
        {
            key: "this-run",
            passed: check.passed,
            label: `this run: ${verdict(check.passed)}`,
            tooltip: `this run · ${verdict(check.passed)}`,
            href: null,
        },
    ];
}

export const AGENT_FAILED_NOTE = "the agent gave no answer";

/** One check of the run with the scenario it belongs to. */
export interface StatedCheck {
    scenarioKey: string;
    check: CheckRunResult;
    state: RegressionState;
}

/** Every check of the run that has a state other than Passed. */
export function statedChecks(results: RunResults): StatedCheck[] {
    return results.scenarios.flatMap((scenario) =>
        scenario.checks.flatMap((check) =>
            check.regression == null || check.regression.state === "Passed"
                ? []
                : [{ scenarioKey: scenario.key, check, state: check.regression.state }],
        ),
    );
}

const ATTENTION_ORDER: readonly RegressionState[] = ["Regression", "New", "AlreadyBroken"];
const BRIEF_ORDER: readonly RegressionState[] = ["Fixed", "Unstable"];

/** What the run page lists: the attention states in order (Regression, New, Already broken), then Fixed and Unstable, known failures apart. */
export function groupChecks(checks: readonly StatedCheck[]): {
    attention: StatedCheck[];
    brief: StatedCheck[];
    known: StatedCheck[];
} {
    const inOrder = (order: readonly RegressionState[]) =>
        checks.filter((c) => order.includes(c.state)).sort((a, b) => order.indexOf(a.state) - order.indexOf(b.state));
    return {
        attention: inOrder(ATTENTION_ORDER),
        brief: inOrder(BRIEF_ORDER),
        known: checks.filter((c) => c.state === "KnownFailure"),
    };
}

/** The chips of the summary: the states with a non-zero count, in the run page's order. */
export function summaryChips(summary: RegressionSummary): { state: RegressionState; label: string; count: number }[] {
    const counts: [RegressionState, number][] = [
        ["Regression", summary.regressions],
        ["New", summary.new],
        ["AlreadyBroken", summary.alreadyBroken],
        ["Fixed", summary.fixed],
        ["Unstable", summary.unstable],
        ["KnownFailure", summary.knownFailures],
    ];
    return counts.filter(([, count]) => count > 0).map(([state, count]) => ({ state, label: stateLabel(state), count }));
}

/** "5 known failures" / "1 known failure". */
export function knownFailuresText(count: number): string {
    return `${count} known failure${count === 1 ? "" : "s"}`;
}
