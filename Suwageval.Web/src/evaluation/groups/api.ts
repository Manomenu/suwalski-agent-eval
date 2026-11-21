import { dataOf } from "@maniumek/suwfrontkit";
import { queryOptions } from "@tanstack/react-query";

import { api } from "../../api/client";
import type { components } from "../../api/openapi";

export type RunGroupSummary = components["schemas"]["RunGroupSummary"];
export type RunGroupDeletion = components["schemas"]["RunGroupDeletion"];

export const runGroupKeys = {
    list: (name: string) => ["agents", name, "run-groups"] as const,
    deletion: (name: string, group: string) => ["agents", name, "run-groups", group, "deletion"] as const,
};

function fetchRunGroups(name: string, signal: AbortSignal): Promise<RunGroupSummary[]> {
    return dataOf(api.GET("/eval/agents/{agent}/run-groups", { params: { path: { agent: name } }, signal }));
}

export function archiveRunGroups(name: string, names: string[], archived: boolean): Promise<void> {
    return dataOf(api.POST("/eval/agents/{agent}/run-groups/archive", { params: { path: { agent: name } }, body: { names, archived } }));
}

export function fetchRunGroupDeletion(name: string, group: string, signal: AbortSignal): Promise<RunGroupDeletion> {
    return dataOf(api.GET("/eval/agents/{agent}/run-groups/{name}/deletion", { params: { path: { agent: name, name: group } }, signal }));
}

export function deleteRunGroup(name: string, group: string): Promise<void> {
    return dataOf(api.DELETE("/eval/agents/{agent}/run-groups/{name}", { params: { path: { agent: name, name: group } } }));
}

export const runGroupsQuery = (name: string) =>
    queryOptions({ queryKey: runGroupKeys.list(name), queryFn: ({ signal }) => fetchRunGroups(name, signal) });
