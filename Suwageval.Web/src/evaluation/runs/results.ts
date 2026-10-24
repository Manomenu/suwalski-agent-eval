// What the run page shows, as plain functions: labels, numbers and which items to highlight.
import { formatSeconds, percent } from "../../format/numbers";
import { versionTitle } from "../../format/names";
import { itemKey } from "../../conversation/items";
import type { CheckRunResult, RunResults, RunSummary, ScenarioRunResult } from "./api";

export const POLL_INTERVAL_MS = 2000;

type Status = RunSummary["status"];

/** What names a run: the label its contract carried when it started, else its version's label, else the start of the version's id. */
export function runLabel(run: Pick<RunSummary, "label" | "agentVersionId">, versionLabel?: string | null): string {
    return run.label?.trim() ? run.label : versionTitle(run.agentVersionId, versionLabel);
}

/** The id of a check's row on the run page, so a link can jump to it (`#check-<scenario>--<check>`). */
export function checkAnchor(scenarioKey: string, checkKey: string): string {
    return `check-${scenarioKey}--${checkKey}`;
}

/** The samples whose output to open when someone jumps to a check: the ones it failed in, else the first. */
export function samplesToOpen(check: Pick<CheckRunResult, "samples">): string[] {
    const failed = check.samples.filter((sample) => !sample.passed).map((sample) => String(sample.sample));
    return failed.length > 0 ? failed : check.samples.slice(0, 1).map((sample) => String(sample.sample));
}

/** A run that is still going: the page asks again until it is over. */
export function isActive(status: Status): boolean {
    return status === "Queued" || status === "Running";
}

type Progress = Pick<RunSummary, "status" | "samplesDone" | "samplesTotal" | "cancelRequested">;

/**
 * While a run goes on the page polls only its summary, and asks for the full results when the summary shows something new since
 * the results on screen: a sample was stored, the status changed (this covers the final status) or a stop was asked for.
 */
export function needsFullResults(shown: Progress, current: Progress): boolean {
    return (
        shown.samplesDone !== current.samplesDone ||
        shown.samplesTotal !== current.samplesTotal ||
        shown.status !== current.status ||
        (shown.cancelRequested ?? false) !== (current.cancelRequested ?? false)
    );
}

/** "Cancel run" is offered while the run is queued or running, and until a stop was asked for. */
export function canCancel(run: Pick<RunSummary, "status" | "cancelRequested">): boolean {
    return isActive(run.status) && run.cancelRequested !== true;
}

/** A stop was asked for, but the samples in flight are still finishing. */
export function isCancelling(run: Pick<RunSummary, "status" | "cancelRequested">): boolean {
    return run.status === "Running" && run.cancelRequested === true;
}

/** The status badge's text: "cancelling…" between the request and the end of the run. */
export function runStatusLabel(run: Pick<RunSummary, "status" | "cancelRequested">): string {
    return isCancelling(run) ? "cancelling…" : statusLabel(run.status);
}

export function statusLabel(status: Status): string {
    switch (status) {
        case "Queued":
            return "queued";
        case "Running":
            return "running";
        case "Done":
            return "done";
        case "Failed":
            return "failed";
        case "Cancelled":
            return "cancelled";
        case "Interrupted":
            return "interrupted";
    }
}

export function statusColor(status: Status): string {
    switch (status) {
        case "Queued":
        case "Running":
            return "blue";
        case "Done":
            return "green";
        case "Failed":
            return "red";
        case "Cancelled":
            return "gray";
        case "Interrupted":
            return "orange";
    }
}

export function checkSummary(check: CheckRunResult): string {
    const passes = check.samples.filter((sample) => sample.passed).length;
    return `${percent(check.passRate)} (${passes}/${check.samples.length} samples, required ${percent(check.passThreshold)})`;
}

/** How many samples of a check no judge could give a verdict on (its model was unreachable or answered unreadably). */
export function notJudgedCount(check: CheckRunResult): number {
    return check.samples.filter((sample) => sample.notJudged).length;
}

/** What the Checks column says on hover, wherever a run's checks are counted. */
export const RUN_CHECKS_HELP =
    "Assertions defined in the scenarios. Each check is judged on every sample of its scenario and passes when enough of those samples pass (its passThreshold, 100% by default).";

/** What the Samples column says on hover, wherever a run's samples are counted. */
export const RUN_SAMPLES_HELP =
    "Runs of the scenarios: each scenario is run `samples` times (3 by default). Answered = the agent returned a reply (no timeout or HTTP error); it says nothing about whether the reply was right.";

/** The checks of a run: `75/97 passed`, and `22 failed` (null when none) for the part shown as a failure. */
export function checksCounts(run: Pick<RunSummary, "checksPassed" | "checksFailed">): { summary: string; problem: string | null } {
    return {
        summary: `${run.checksPassed}/${run.checksPassed + run.checksFailed} passed`,
        problem: run.checksFailed > 0 ? `${run.checksFailed} failed` : null,
    };
}

/** The samples of a run: `130/132 answered` (just `130 answered` before the total is known), and `2 no answer` (null when none). */
export function samplesCounts(run: Pick<RunSummary, "samplesTotal" | "samplesDone" | "samplesFailed">): {
    summary: string;
    problem: string | null;
} {
    const failed = run.samplesFailed ?? 0;
    const answered = run.samplesDone - failed;
    return {
        summary: run.samplesTotal > 0 ? `${answered}/${run.samplesTotal} answered` : `${answered} answered`,
        problem: failed > 0 ? `${failed} no answer` : null,
    };
}

/** The checks of these scenarios as the server counts a run's: each check once, passed when its pass rate reached its threshold. */
export function scenariosChecksCounts(scenarios: readonly ScenarioRunResult[]): Pick<RunSummary, "checksPassed" | "checksFailed"> {
    const checks = scenarios.flatMap((scenario) => scenario.checks);
    const checksPassed = checks.filter((check) => check.passed).length;
    return { checksPassed, checksFailed: checks.length - checksPassed };
}

/** The samples of these scenarios, all of them done: a run's total is not known per scenario. */
export function scenariosSamplesCounts(
    scenarios: readonly ScenarioRunResult[],
): Pick<RunSummary, "samplesTotal" | "samplesDone" | "samplesFailed"> {
    const samples = scenarios.flatMap((scenario) => scenario.samples);
    return {
        samplesTotal: samples.length,
        samplesDone: samples.length,
        samplesFailed: samples.filter((sample) => sample.status === "Failed").length,
    };
}

/** The items failed checks point at for one sample — to highlight them in the agent's output. */
export function failedEvidenceKeys(scenario: ScenarioRunResult, sample: number): Set<string> {
    const keys = new Set<string>();
    for (const check of scenario.checks) {
        for (const checkSample of check.samples) {
            if (checkSample.sample === sample && !checkSample.passed) {
                for (const item of checkSample.evidence) keys.add(itemKey(item));
            }
        }
    }
    return keys;
}

export interface SampleVerdict {
    kind: "passed" | "failed" | "not judged" | "no output" | "unchecked";
    failedChecks: number;
}

/** What the checks said of one sample of a scenario: failed when any check failed in it, else not judged when any could not be judged. */
export function sampleVerdict(scenario: ScenarioRunResult, sample: number): SampleVerdict {
    const verdicts = scenario.checks.flatMap((check) => check.samples.filter((checkSample) => checkSample.sample === sample));
    const failedChecks = verdicts.filter((verdict) => !verdict.passed && !verdict.notJudged).length;
    if (failedChecks > 0) return { kind: "failed", failedChecks };
    if (verdicts.some((verdict) => verdict.notJudged)) return { kind: "not judged", failedChecks };
    if (scenario.samples.find((candidate) => candidate.sample === sample)?.status === "Failed") return { kind: "no output", failedChecks };
    return { kind: verdicts.length > 0 ? "passed" : "unchecked", failedChecks };
}

/** "Turn time: median 9.8 s · p95 109.1 s" over all samples of the run that made a call; null when none did. `label` replaces "Turn time". */
export function durationSummary(results: Pick<RunResults, "medianMs" | "p95Ms">, label = "Turn time"): string | null {
    const { medianMs, p95Ms } = results;
    return medianMs == null || p95Ms == null ? null : `${label}: median ${formatSeconds(medianMs)} · p95 ${formatSeconds(p95Ms)}`;
}
