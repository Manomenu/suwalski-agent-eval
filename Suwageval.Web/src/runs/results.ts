// What the run page shows, as plain functions: labels, numbers and which items to highlight.
import type { CheckRunResult, ConversationItem, RunSummary, ScenarioRunResult } from "./api";

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

/** A run that is still going: the page asks again until it is over. */
export function isActive(status: Status): boolean {
    return status === "Queued" || status === "Running";
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
        default:
            return "?";
    }
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
        default:
            return "";
    }
}

/** Milliseconds as seconds rounded to one decimal: 9840 → 9.8. */
export function seconds(milliseconds: number): number {
    return Math.round(milliseconds / 100) / 10;
}

/** 9800 → "9.8 s" (one decimal). */
export function formatSeconds(milliseconds: number): string {
    return `${seconds(milliseconds).toFixed(1)} s`;
}

/** How long the agent took for each sample that made a call (the samples without one have no duration). */
function sampleDurations(scenarios: readonly ScenarioRunResult[]): number[] {
    return scenarios.flatMap((scenario) => scenario.samples.flatMap((sample) => (sample.durationMs == null ? [] : [sample.durationMs])));
}

/** The middle value; the mean of the two middle ones for an even count. */
export function median(values: readonly number[]): number {
    const sorted = [...values].sort((a, b) => a - b);
    const middle = Math.floor(sorted.length / 2);
    return sorted.length % 2 === 1 ? (sorted[middle] ?? 0) : ((sorted[middle - 1] ?? 0) + (sorted[middle] ?? 0)) / 2;
}

/** The 95th percentile by nearest rank: the value at rank ⌈0.95·n⌉ of the sorted values. */
export function p95(values: readonly number[]): number {
    const sorted = [...values].sort((a, b) => a - b);
    return sorted[Math.max(1, Math.ceil(0.95 * sorted.length)) - 1] ?? 0;
}

/** The median and p95 turn time over all samples that made a call, in milliseconds; null when none did. */
export function turnTimes(scenarios: readonly ScenarioRunResult[]): { medianMs: number; p95Ms: number } | null {
    const durations = sampleDurations(scenarios);
    if (durations.length === 0) return null;
    return { medianMs: median(durations), p95Ms: p95(durations) };
}

/** "Turn time: median 9.8 s · p95 109.1 s" over all samples that made a call; null when none did. */
export function durationSummary(scenarios: readonly ScenarioRunResult[]): string | null {
    const times = turnTimes(scenarios);
    return times === null ? null : `Turn time: median ${formatSeconds(times.medianMs)} · p95 ${formatSeconds(times.p95Ms)}`;
}
