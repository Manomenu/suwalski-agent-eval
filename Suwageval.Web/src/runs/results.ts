// What the run page shows, as plain functions: labels, numbers and which items to highlight.
import type { CheckRunResult, ConversationItem, RunResults, RunSummary, ScenarioRunResult } from "./api";

export const POLL_INTERVAL_MS = 2000;

type Status = RunSummary["status"];

/** The page of a run. */
export const runHref = (runId: string) => `/runs/${runId}`;

/** The first eight characters of a guid — enough to tell runs and versions apart in a table. */
export function shortId(id: string): string {
    return id.slice(0, 8);
}

/** What names a version in its page's title: its label, or the start of its id when it has none. */
export function versionTitle(id: string, label: string | null | undefined): string {
    return label?.trim() ? label : shortId(id);
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
    }
}

/** 0.666 → 66.7: a share as a percentage rounded to one decimal. */
export function percentValue(share: number): number {
    return Math.round(share * 1000) / 10;
}

/** 0.666 → "66.7%" (no trailing zeros, like the CLI report). */
export function percent(share: number): string {
    return `${percentValue(share)}%`;
}

export function formatTime(iso: string | null | undefined): string {
    if (!iso) return "—";
    return new Date(iso).toLocaleString("en-GB");
}

export function checkSummary(check: CheckRunResult): string {
    const passes = check.samples.filter((sample) => sample.passed).length;
    return `${percent(check.passRate)} (${passes}/${check.samples.length} samples, required ${percent(check.passThreshold)})`;
}

/** How many samples of a check no judge could give a verdict on (its model was unreachable or answered unreadably). */
export function notJudgedCount(check: CheckRunResult): number {
    return check.samples.filter((sample) => sample.notJudged).length;
}

/** A run's progress: `3/10`, or just `0` before the total is known. */
export function progress(run: RunSummary): string {
    const total = run.samplesTotal;
    return total > 0 ? `${run.samplesDone}/${total}` : `${run.samplesDone}`;
}

export function itemKey(item: ConversationItem): string {
    return JSON.stringify(item);
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

export function itemTitle(item: ConversationItem): string {
    switch (item.kind) {
        case "UserMessage":
            return "user";
        case "AssistantMessage":
            return item.interrupted === true ? "agent (interrupted)" : "agent";
        case "ToolCall":
            return `call: ${item.name}`;
        case "ToolResult":
            return item.isError === true ? "tool result (error)" : "tool result";
        case "Retrieval":
            return `retrieval: ${item.query}`;
        case "Event":
            return `event: ${item.type}`;
        case undefined:
            break;
    }
    return "?";
}

/** The body of an item as text: message text, pretty arguments, a result, retrieved chunks. */
export function itemBody(item: ConversationItem): string {
    switch (item.kind) {
        case "UserMessage":
        case "AssistantMessage":
            return item.text;
        case "ToolCall":
            return JSON.stringify(item.arguments, null, 2);
        case "ToolResult":
            return item.content;
        case "Retrieval":
            return item.chunks
                .map((chunk) => `[${chunk.source}${chunk.score == null ? "" : `, ${chunk.score.toFixed(2)}`}] ${chunk.text}`)
                .join("\n\n");
        case "Event":
            return JSON.stringify(item.data, null, 2);
        case undefined:
            break;
    }
    return "";
}

/** Milliseconds as seconds rounded to one decimal: 9840 → 9.8. */
export function seconds(milliseconds: number): number {
    return Math.round(milliseconds / 100) / 10;
}

/** 9800 → "9.8 s" (one decimal). */
export function formatSeconds(milliseconds: number): string {
    return `${seconds(milliseconds).toFixed(1)} s`;
}

/** "Turn time: median 9.8 s · p95 109.1 s" over all samples of the run that made a call; null when none did. */
export function durationSummary(results: Pick<RunResults, "medianMs" | "p95Ms">): string | null {
    const { medianMs, p95Ms } = results;
    return medianMs == null || p95Ms == null ? null : `Turn time: median ${formatSeconds(medianMs)} · p95 ${formatSeconds(p95Ms)}`;
}
