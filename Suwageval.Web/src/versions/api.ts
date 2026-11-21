import { dataOf } from "@maniumek/suwfrontkit";
import { queryOptions } from "@tanstack/react-query";

import { agentKeys } from "../agents/api";
import { api } from "../api/client";
import type { components } from "../api/openapi";

export type VersionDeletion = components["schemas"]["VersionDeletion"];
export type VersionDiff = components["schemas"]["VersionDiff"];

export async function archiveVersions(name: string, versionIds: string[], archived: boolean): Promise<void> {
    const body: components["schemas"]["ArchiveVersionsRequest"] = { versionIds, archived };
    await dataOf(api.POST("/agents/{agent}/versions/archive", { params: { path: { agent: name } }, body }));
}

export function fetchVersionDeletion(name: string, versionId: string, signal: AbortSignal): Promise<VersionDeletion> {
    return dataOf(api.GET("/agents/{agent}/versions/{versionId}/deletion", { params: { path: { agent: name, versionId } }, signal }));
}

export async function deleteVersion(name: string, versionId: string): Promise<void> {
    await dataOf(api.DELETE("/agents/{agent}/versions/{versionId}", { params: { path: { agent: name, versionId } } }));
}

function fetchVersionDiff(name: string, from: string, to: string, signal: AbortSignal): Promise<VersionDiff> {
    return dataOf(api.GET("/agents/{agent}/versions/diff", { params: { path: { agent: name }, query: { from, to } }, signal }));
}

export const versionDiffQuery = (name: string, from: string, to: string) =>
    queryOptions({
        queryKey: [...agentKeys.agent(name), "diff", from, to] as const,
        queryFn: ({ signal }) => fetchVersionDiff(name, from, to, signal),
    });
