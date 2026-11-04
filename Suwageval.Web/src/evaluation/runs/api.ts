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

export function fetchRunSummary(runId: string, signal: AbortSignal): Promise<RunSummary> {
    return request<RunSummary>(`/eval/runs/${runId}`, { signal });
}

export function cancelRun(runId: string): Promise<void> {
    return request<undefined>(`/eval/runs/${runId}/cancel`, { method: "POST" }).then(() => undefined);
}

export function setRunNote(runId: string, note: string | null): Promise<void> {
    return request<undefined>(`/eval/runs/${runId}/note`, { method: "PUT", body: JSON.stringify({ note }) }).then(() => undefined);
}

export function runRequestLike(run: RunSummary): RunRequest {
    return { agent: run.agent, tags: run.tags ?? [], endpointUrl: run.endpointUrl, group: run.group ?? null, origin: run.origin ?? null };
}

export function startRun(body: RunRequest): Promise<RunCreated> {
    return request<RunCreated>("/eval/runs", { method: "POST", body: JSON.stringify(body) });
}

export function fetchRecheckPlan(runId: string, signal: AbortSignal): Promise<RecheckPlan> {
    return request<RecheckPlan>(`/eval/runs/${runId}/recheck`, { signal });
}

export function recheck(runId: string): Promise<RunCreated> {
    return request<RunCreated>(`/eval/runs/${runId}/recheck`, { method: "POST" });
}
