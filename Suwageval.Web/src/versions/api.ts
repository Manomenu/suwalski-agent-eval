import { request } from "../api/client";
import type { components } from "../api/openapi";
import { agentPath } from "../agents/api";

export async function archiveVersions(name: string, versionIds: string[], archived: boolean): Promise<void> {
    const body: components["schemas"]["ArchiveVersionsRequest"] = { versionIds, archived };
    await request<undefined>(`${agentPath(name)}/versions/archive`, { method: "POST", body: JSON.stringify(body) });
}

export type VersionDeletion = components["schemas"]["VersionDeletion"];

export function fetchVersionDeletion(name: string, versionId: string, signal: AbortSignal): Promise<VersionDeletion> {
    return request<VersionDeletion>(`${agentPath(name)}/versions/${versionId}/deletion`, { signal });
}

export async function deleteVersion(name: string, versionId: string): Promise<void> {
    await request<undefined>(`${agentPath(name)}/versions/${versionId}`, { method: "DELETE" });
}

export type VersionDiff = components["schemas"]["VersionDiff"];

export function fetchVersionDiff(name: string, from: string, to: string, signal: AbortSignal): Promise<VersionDiff> {
    const query = new URLSearchParams({ from, to });
    return request<VersionDiff>(`${agentPath(name)}/versions/diff?${query.toString()}`, { signal });
}
