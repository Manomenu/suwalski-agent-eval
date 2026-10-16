// This feature's HTTP calls, typed from the generated OpenAPI types.
import { request } from "../api/client";
import type { components } from "../api/openapi";

export type RunResults = components["schemas"]["RunResults"];
export type RunCreated = components["schemas"]["RunCreated"];
export type RunSummary = components["schemas"]["RunSummary"];
export type ScenarioRunResult = components["schemas"]["ScenarioRunResult"];
export type CheckRunResult = components["schemas"]["CheckRunResult"];
export type ConversationItem = components["schemas"]["ConversationItem"];

/** The runs of one agent (by name), newest first. */
export function fetchRuns(agent: string, signal: AbortSignal): Promise<RunSummary[]> {
    return request<RunSummary[]>(`/runs?agent=${encodeURIComponent(agent)}`, { signal });
}

export function fetchRunResults(runId: string, signal: AbortSignal): Promise<RunResults> {
    return request<RunResults>(`/runs/${runId}/results`, { signal });
}

/** Asks the server to stop the run: a queued one is cancelled at once, a running one when its samples in flight are done. */
export function cancelRun(runId: string): Promise<void> {
    return request<undefined>(`/runs/${runId}/cancel`, { method: "POST" }).then(() => undefined);
}

/** A new run with the agent, tag and endpoint of this one, against the version the agent serves now. */
export function rerun(runId: string): Promise<RunCreated> {
    return request<RunCreated>(`/runs/${runId}/rerun`, { method: "POST" });
}
