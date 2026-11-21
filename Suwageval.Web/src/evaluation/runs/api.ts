import { dataOf } from "@maniumek/suwfrontkit";
import { queryOptions } from "@tanstack/react-query";

import { api } from "../../api/client";
import type { components } from "../../api/openapi";
import { runsQuery } from "./paging";

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

export const runKeys = {
    all: ["runs"] as const,
    list: (
        agent: string,
        page: number,
        pageSize: number,
        versionIds: readonly string[],
        group: string | null,
        grouping: "None" | "Any" | null,
        tags: readonly string[],
        dataVersions: readonly string[],
    ) => ["runs", "list", agent, page, pageSize, versionIds, group, grouping, tags, dataVersions] as const,
    results: (id: string) => ["runs", id, "results"] as const,
    recheckPlan: (id: string) => ["runs", id, "recheck"] as const,
};

function fetchRuns(
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
    return dataOf(
        api.GET("/eval/runs", {
            params: { query: runsQuery(agent, page, pageSize, versionIds, group, grouping, tags, dataVersions) },
            signal,
        }),
    );
}

export function fetchRunResults(runId: string, signal: AbortSignal): Promise<RunResults> {
    return dataOf(api.GET("/eval/runs/{id}/results", { params: { path: { id: runId } }, signal }));
}

export function fetchRunSummary(runId: string, signal: AbortSignal): Promise<RunSummary> {
    return dataOf(api.GET("/eval/runs/{id}", { params: { path: { id: runId } }, signal }));
}

export function cancelRun(runId: string): Promise<void> {
    return dataOf(api.POST("/eval/runs/{id}/cancel", { params: { path: { id: runId } } }));
}

export function setRunNote(runId: string, note: string | null): Promise<void> {
    return dataOf(api.PUT("/eval/runs/{id}/note", { params: { path: { id: runId } }, body: { note } }));
}

export function runRequestLike(run: RunSummary): RunRequest {
    return { agent: run.agent, tags: run.tags ?? [], endpointUrl: run.endpointUrl, group: run.group ?? null, origin: run.origin ?? null };
}

export function startRun(body: RunRequest): Promise<RunCreated> {
    return dataOf(api.POST("/eval/runs", { body }));
}

function fetchRecheckPlan(runId: string, signal: AbortSignal): Promise<RecheckPlan> {
    return dataOf(api.GET("/eval/runs/{id}/recheck", { params: { path: { id: runId } }, signal }));
}

export function recheck(runId: string): Promise<RunCreated> {
    return dataOf(api.POST("/eval/runs/{id}/recheck", { params: { path: { id: runId } } }));
}

export const runListQuery = (
    agent: string,
    page: number,
    pageSize: number,
    versionIds: readonly string[],
    group: string | null,
    grouping: "None" | "Any" | null,
    tags: readonly string[],
    dataVersions: readonly string[],
) =>
    queryOptions({
        queryKey: runKeys.list(agent, page, pageSize, versionIds, group, grouping, tags, dataVersions),
        queryFn: ({ signal }) => fetchRuns(agent, page, pageSize, versionIds, group, grouping, tags, dataVersions, signal),
    });

export const recheckPlanQuery = (runId: string) =>
    queryOptions({ queryKey: runKeys.recheckPlan(runId), queryFn: ({ signal }) => fetchRecheckPlan(runId, signal) });
