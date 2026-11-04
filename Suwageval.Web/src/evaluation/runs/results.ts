import { formatSeconds, percent } from "../../format/numbers";
import { versionTitle } from "../../format/names";
import { failedEvidence } from "../../verdict/verdict";
import type { CheckRunResult, RunResults, RunSummary, ScenarioRunResult } from "./api";

export const POLL_INTERVAL_MS = 2000;

type Status = RunSummary["status"];

export function runLabel(run: Pick<RunSummary, "label" | "agentVersionId">, versionLabel?: string | null): string {
    return run.label?.trim() ? run.label : versionTitle(run.agentVersionId, versionLabel);
}

export function checkAnchor(scenarioKey: string, checkKey: string): string {
    return `check-${scenarioKey}--${checkKey}`;
}

export function samplesToOpen(check: Pick<CheckRunResult, "samples">): string[] {
    const failed = check.samples.filter((sample) => !sample.passed).map((sample) => String(sample.sample));
    return failed.length > 0 ? failed : check.samples.slice(0, 1).map((sample) => String(sample.sample));
}

export function isActive(status: Status): boolean {
    return status === "Queued" || status === "Running";
}

type Progress = Pick<RunSummary, "status" | "samplesDone" | "samplesTotal" | "cancelRequested">;

export function needsFullResults(shown: Progress, current: Progress): boolean {
    return (
        shown.samplesDone !== current.samplesDone ||
        shown.samplesTotal !== current.samplesTotal ||
        shown.status !== current.status ||
        (shown.cancelRequested ?? false) !== (current.cancelRequested ?? false)
    );
}

export function canCancel(run: Pick<RunSummary, "status" | "cancelRequested">): boolean {
    return isActive(run.status) && run.cancelRequested !== true;
}

export function isCancelling(run: Pick<RunSummary, "status" | "cancelRequested">): boolean {
    return run.status === "Running" && run.cancelRequested === true;
}

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

export function notJudgedCount(check: CheckRunResult): number {
    return check.samples.filter((sample) => sample.notJudged).length;
}

export const RUN_CHECKS_HELP =
    "Assertions defined in the scenarios. Each check is judged on every sample of its scenario and passes when enough of those samples pass (its passThreshold, 100% by default).";

export const RUN_SAMPLES_HELP =
    "Runs of the scenarios: each scenario is run `samples` times (3 by default). Answered = the agent returned a reply (no timeout or HTTP error); it says nothing about whether the reply was right.";

export function checksCounts(run: Pick<RunSummary, "checksPassed" | "checksFailed">): { summary: string; problem: string | null } {
    return {
        summary: `${run.checksPassed}/${run.checksPassed + run.checksFailed} passed`,
        problem: run.checksFailed > 0 ? `${run.checksFailed} failed` : null,
    };
}

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

export function scenariosChecksCounts(scenarios: readonly ScenarioRunResult[]): Pick<RunSummary, "checksPassed" | "checksFailed"> {
    const checks = scenarios.flatMap((scenario) => scenario.checks);
    const checksPassed = checks.filter((check) => check.passed).length;
    return { checksPassed, checksFailed: checks.length - checksPassed };
}

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

export function failedEvidenceKeys(scenario: ScenarioRunResult, sample: number): Set<string> {
    return failedEvidence(scenario.checks.flatMap((check) => check.samples.filter((checkSample) => checkSample.sample === sample)));
}

export interface SampleVerdict {
    kind: "passed" | "failed" | "not judged" | "no output" | "unchecked";
    failedChecks: number;
}

export function sampleVerdict(scenario: ScenarioRunResult, sample: number): SampleVerdict {
    const verdicts = scenario.checks.flatMap((check) => check.samples.filter((checkSample) => checkSample.sample === sample));
    const failedChecks = verdicts.filter((verdict) => !verdict.passed && !verdict.notJudged).length;
    if (failedChecks > 0) return { kind: "failed", failedChecks };
    if (verdicts.some((verdict) => verdict.notJudged)) return { kind: "not judged", failedChecks };
    if (scenario.samples.find((candidate) => candidate.sample === sample)?.status === "Failed") return { kind: "no output", failedChecks };
    return { kind: verdicts.length > 0 ? "passed" : "unchecked", failedChecks };
}

export function durationSummary(results: Pick<RunResults, "medianMs" | "p95Ms">, label = "Turn time"): string | null {
    const { medianMs, p95Ms } = results;
    return medianMs == null || p95Ms == null ? null : `${label}: median ${formatSeconds(medianMs)} · p95 ${formatSeconds(p95Ms)}`;
}
