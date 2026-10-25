// This feature's HTTP calls, typed from the generated OpenAPI types.
import { request } from "../../api/client";
import type { components } from "../../api/openapi";
import { runsPath } from "./paging";

export type RunResults = components["schemas"]["RunResults"];
export type RunCreated = components["schemas"]["RunCreated"];
export type RunList = components["schemas"]["RunList"];
export type RunSummary = components["schemas"]["RunSummary"];
export type RunRequest = components["schemas"]["RunRequest"];
export type ScenarioRunResult = components["schemas"]["ScenarioRunResult"];
export type SampleResult = components["schemas"]["SampleResult"];
export type CheckRunResult = components["schemas"]["CheckRunResult"];
export type RecheckPlan = components["schemas"]["RecheckPlan"];
export type ChangedCheck = components["schemas"]["ChangedCheck"];
export type CheckRegression = components["schemas"]["CheckRegression"];
export type RegressionState = components["schemas"]["RegressionState"];
export type RegressionSummary = components["schemas"]["RegressionSummary"];
export type ConversationItem = components["schemas"]["ConversationItem"];

/** One page of the runs of one agent (by name), newest first; see `runsPath`. */
export function fetchRuns(
    agent: string,
    page: number,
    pageSize: number,
    versionIds: readonly string[],
    group: string | null,
    grouping: "None" | "Any" | null,
    tags: readonly string[],
    dataVersions: readonly string[],
    signal: AbortSignal,
): Promise<RunList> {
    return request<RunList>(runsPath(agent, page, pageSize, versionIds, group, grouping, tags, dataVersions), { signal });
}

export function fetchRunResults(runId: string, signal: AbortSignal): Promise<RunResults> {
    return request<RunResults>(`/eval/runs/${runId}/results`, { signal });
}

/** The small summary of a run: what the page polls while the run goes on, instead of the full results. */
export function fetchRunSummary(runId: string, signal: AbortSignal): Promise<RunSummary> {
    return request<RunSummary>(`/eval/runs/${runId}`, { signal });
}

/** Asks the server to stop the run: a queued one is cancelled at once, a running one when its samples in flight are done. */
export function cancelRun(runId: string): Promise<void> {
    return request<undefined>(`/eval/runs/${runId}/cancel`, { method: "POST" }).then(() => undefined);
}

/** Sets the note of the run (why it was started); none or blank clears it. Any status. */
export function setRunNote(runId: string, note: string | null): Promise<void> {
    return request<undefined>(`/eval/runs/${runId}/note`, { method: "PUT", body: JSON.stringify({ note }) }).then(() => undefined);
}

/** The start of a new run like `run`: its agent, tags, endpoint, group and origin, against the version the agent serves now. */
export function runRequestLike(run: RunSummary): RunRequest {
    return { agent: run.agent, tags: run.tags ?? [], endpointUrl: run.endpointUrl, group: run.group ?? null, origin: run.origin ?? null };
}

/** Starts a new run (the same start as `suwageval run`). */
export function startRun(body: RunRequest): Promise<RunCreated> {
    return request<RunCreated>("/eval/runs", { method: "POST", body: JSON.stringify(body) });
}

/** What a re-check of the run would do and whether it can start; nothing is started. */
export function fetchRecheckPlan(runId: string, signal: AbortSignal): Promise<RecheckPlan> {
    return request<RecheckPlan>(`/eval/runs/${runId}/recheck`, { signal });
}

/** A new run that judges this run's stored answers again with the current checks; the agent is not called. */
export function recheck(runId: string): Promise<RunCreated> {
    return request<RunCreated>(`/eval/runs/${runId}/recheck`, { method: "POST" });
}
