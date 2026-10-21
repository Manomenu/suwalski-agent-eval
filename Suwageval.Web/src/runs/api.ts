// This feature's HTTP calls, typed from the generated OpenAPI types.
import { request } from "../api/client";
import type { components } from "../api/openapi";
import { runsPath } from "./paging";
import { unstableChecksPath } from "./stability";

export type RunResults = components["schemas"]["RunResults"];
export type RunCreated = components["schemas"]["RunCreated"];
export type RunList = components["schemas"]["RunList"];
export type RunSummary = components["schemas"]["RunSummary"];
export type ScenarioRunResult = components["schemas"]["ScenarioRunResult"];
export type SampleResult = components["schemas"]["SampleResult"];
export type CheckRunResult = components["schemas"]["CheckRunResult"];
export type UnstableCheck = components["schemas"]["UnstableCheck"];
export type RecheckPlan = components["schemas"]["RecheckPlan"];
export type ChangedCheck = components["schemas"]["ChangedCheck"];
export type CheckRegression = components["schemas"]["CheckRegression"];
export type RegressionState = components["schemas"]["RegressionState"];
export type RegressionSummary = components["schemas"]["RegressionSummary"];
export type ConversationItem = components["schemas"]["ConversationItem"];

/** One page of the runs of one agent (by name), newest first; see `runsPath`. */
export function fetchRuns(
    agent: string,
    before: string | null,
    versionIds: readonly string[],
    group: string | null,
    signal: AbortSignal,
): Promise<RunList> {
    return request<RunList>(runsPath(agent, before, versionIds, group), { signal });
}

export function fetchRunResults(runId: string, signal: AbortSignal): Promise<RunResults> {
    return request<RunResults>(`/runs/${runId}/results`, { signal });
}

/** The small summary of a run: what the page polls while the run goes on, instead of the full results. */
export function fetchRunSummary(runId: string, signal: AbortSignal): Promise<RunSummary> {
    return request<RunSummary>(`/runs/${runId}`, { signal });
}

/** Asks the server to stop the run: a queued one is cancelled at once, a running one when its samples in flight are done. */
export function cancelRun(runId: string): Promise<void> {
    return request<undefined>(`/runs/${runId}/cancel`, { method: "POST" }).then(() => undefined);
}

/** A new run with the agent, tag and endpoint of this one, against the version the agent serves now. */
export function rerun(runId: string): Promise<RunCreated> {
    return request<RunCreated>(`/runs/${runId}/rerun`, { method: "POST" });
}

/** The agent's unstable checks (see `stability.ts`), limited to the given versions (none: every version not archived). */
export function fetchUnstableChecks(agent: string, versionIds: readonly string[], signal: AbortSignal): Promise<UnstableCheck[]> {
    return request<UnstableCheck[]>(unstableChecksPath(agent, versionIds), { signal });
}

/** What a re-check of the run would do and whether it can start; nothing is started. */
export function fetchRecheckPlan(runId: string, signal: AbortSignal): Promise<RecheckPlan> {
    return request<RecheckPlan>(`/runs/${runId}/recheck`, { signal });
}

/** A new run that judges this run's stored answers again with the current checks; the agent is not called. */
export function recheck(runId: string): Promise<RunCreated> {
    return request<RunCreated>(`/runs/${runId}/recheck`, { method: "POST" });
}
