// This sub-feature's HTTP calls, typed from the generated OpenAPI types.
import { request } from "../api/client";
import type { components } from "../api/openapi";
import { agentPath } from "../agents/api";

/** Archives (`archived: true`) or restores versions of the agent; nothing is deleted. */
export async function archiveVersions(name: string, versionIds: string[], archived: boolean): Promise<void> {
    const body: components["schemas"]["ArchiveVersionsRequest"] = { versionIds, archived };
    await request<undefined>(`${agentPath(name)}/versions/archive`, { method: "POST", body: JSON.stringify(body) });
}

export type VersionDeletion = components["schemas"]["VersionDeletion"];

/** What deleting the version would remove; the server refuses like the deletion would (409: not archived, or a run in progress). */
export function fetchVersionDeletion(name: string, versionId: string, signal: AbortSignal): Promise<VersionDeletion> {
    return request<VersionDeletion>(`${agentPath(name)}/versions/${versionId}/deletion`, { signal });
}

/** Deletes an archived version with its runs, samples and verdicts. */
export async function deleteVersion(name: string, versionId: string): Promise<void> {
    await request<undefined>(`${agentPath(name)}/versions/${versionId}`, { method: "DELETE" });
}

export type VersionDiff = components["schemas"]["VersionDiff"];

/** What changed between two versions, the older first: both contracts as YAML compared by lines, computed by the server. */
export function fetchVersionDiff(name: string, from: string, to: string, signal: AbortSignal): Promise<VersionDiff> {
    const query = new URLSearchParams({ from, to });
    return request<VersionDiff>(`${agentPath(name)}/versions/diff?${query.toString()}`, { signal });
}
