// What a check's state against its history means for the pages (the server decides the state): labels, colours, the order the run
// page lists them in and the links to follow.
import type { CheckRegression, CheckRunResult, RegressionState, RegressionSummary, RunResults } from "./api";
import { compareHref, runHref } from "../routes/links";
import { shortId } from "../format/names";

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
        case "AgentError":
            return "Agent error";
    }
}

/** What the state means, for someone who meets it for the first time (the tooltip of its badge). */
export function stateExplanation(state: RegressionState): string {
    switch (state) {
        case "Passed":
            return "Passes now, steadily or with too little history to judge.";
        case "Regression":
            return "Fails now after passing in every one of the newest reference runs: most likely broken by what changed since. Fails the CLI's exit code.";
        case "New":
            return "Fails now, and the check has too little history to tell whether it ever passed (a new check or scenario, or a changed one starts its history again). Fails the CLI's exit code.";
        case "AlreadyBroken":
            return "Fails now, but it started failing in an earlier reference run after passing steadily: not caused by this run. See since when.";
        case "KnownFailure":
            return "Fails now and has failed in every one of the newest reference runs: a long-standing failure, not caused by this run.";
        case "Fixed":
            return "Passes now after failing in the reference runs before it.";
        case "Unstable":
            return "Its history mixes passes and failures with no steady pattern, so a failure may be noise. Does not fail the CLI's exit code.";
        case "AgentError":
            return "The agent gave no answer in this scenario, so the check was not evaluated. It says nothing about the check, and the run is not counted in the check's history. Fails the CLI's exit code.";
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
        case "AgentError":
            return "grape";
    }
}

/** "Compared with master" / "Compared with every run of the agent" (a run without git). */
export function comparedWithHeading(summary: RegressionSummary): string {
    return summary.comparedWith == null ? "Compared with every run of the agent" : `Compared with ${summary.comparedWith}`;
}

/** The history in words: "last 5 reference runs" (or "last 5 runs" without git). */
export function historyText(summary: RegressionSummary): string {
    const runs = summary.comparedWith == null ? "runs" : "reference runs";
    // The state reads up to twice that many runs (a failure after a steady pass needs both), and so does the history strip.
    return `${summary.history} ${runs} make a steady pass; the history shows up to ${summary.history * 2}`;
}

/** The compare page of the last run the check passed in and this one, or null when it never passed in the history. */
export function regressionCompareHref(runId: string, lastPassedRun: string | null | undefined): string | null {
    return lastPassedRun == null ? null : compareHref([lastPassedRun, runId], lastPassedRun);
}

/**
 * The link under a check of a run: Fixed compares with the last run the check failed in, the other states with the last run it
 * passed in (an already broken check has none: it was not this run that broke it). Null when there is no such run.
 */
export function compareLink(runId: string, regression: CheckRegression | null | undefined): { label: string; href: string } | null {
    if (regression == null || regression.state === "AlreadyBroken") return null;
    if (regression.state === "Fixed") {
        const failed = [...(regression.history ?? [])].reverse().find((point) => !point.passed);
        return failed === undefined
            ? null
            : { label: "Compare with the last failing run", href: compareHref([failed.runId, runId], failed.runId) };
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
    outcome: "passed" | "failed" | "agentError";
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
    const wording = { passed: "passed", failed: "failed", agentError: "agent error" } as const;
    const current = check.regression?.state === "AgentError" ? "agentError" : check.passed ? "passed" : "failed";
    return [
        ...history.map((point) => {
            const outcome = point.passed ? "passed" : "failed";
            return {
                key: point.runId,
                outcome,
                label: `run ${shortId(point.runId)}: ${wording[outcome]}`,
                tooltip: `${historyTime(point.createdAt)} · run ${shortId(point.runId)} · ${wording[outcome]}`,
                href: runHref(point.runId),
            } satisfies HistoryMark;
        }),
        {
            key: "this-run",
            outcome: current,
            label: `this run: ${wording[current]}`,
            tooltip: `this run · ${wording[current]}`,
            href: null,
        },
    ];
}

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

const LISTED_ORDER: readonly RegressionState[] = ["Regression", "New", "AgentError", "AlreadyBroken", "Fixed", "Unstable"];

/** What the run page lists: a group per state in the page's order (Regression, New, Agent error, Already broken, Fixed, Unstable; empty ones left out), known failures apart. */
export function groupChecks(checks: readonly StatedCheck[]): {
    groups: { state: RegressionState; checks: StatedCheck[] }[];
    known: StatedCheck[];
} {
    return {
        groups: LISTED_ORDER.map((state) => ({ state, checks: checks.filter((c) => c.state === state) })).filter(
            (group) => group.checks.length > 0,
        ),
        known: checks.filter((c) => c.state === "KnownFailure"),
    };
}
