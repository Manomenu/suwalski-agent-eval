// What a check's state against its history means for the pages (the server decides the state): labels, colours, the order the run
// page lists them in and the links to follow.
import type { CheckRunResult, RegressionState, RegressionSummary, RunResults } from "./api";

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
