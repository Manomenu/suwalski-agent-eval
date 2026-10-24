// This sub-feature's HTTP calls, typed from the generated OpenAPI types.
import { request } from "../../api/client";
import type { components } from "../../api/openapi";
import { agentPath } from "../../agents/api";

export type RunGroupSummary = components["schemas"]["RunGroupSummary"];

/** The groups of the agent's runs (run --group), the one with the newest run first. */
export function fetchRunGroups(name: string, signal: AbortSignal): Promise<RunGroupSummary[]> {
    return request<RunGroupSummary[]>(`${agentPath(name)}/run-groups`, { signal });
}

/** Archives (`archived: true`) or restores run groups of the agent, by name; nothing is deleted. */
export async function archiveRunGroups(name: string, names: string[], archived: boolean): Promise<void> {
    const body: components["schemas"]["ArchiveRunGroupsRequest"] = { names, archived };
    await request<undefined>(`${agentPath(name)}/run-groups/archive`, { method: "POST", body: JSON.stringify(body) });
}

export type RunGroupDeletion = components["schemas"]["RunGroupDeletion"];

/** What deleting the group would remove; the server refuses like the deletion would (409: not archived, or a run in progress). */
export function fetchRunGroupDeletion(name: string, group: string, signal: AbortSignal): Promise<RunGroupDeletion> {
    return request<RunGroupDeletion>(`${agentPath(name)}/run-groups/${encodeURIComponent(group)}/deletion`, { signal });
}

/** Deletes an archived run group with its runs, samples and verdicts. */
export async function deleteRunGroup(name: string, group: string): Promise<void> {
    await request<undefined>(`${agentPath(name)}/run-groups/${encodeURIComponent(group)}`, { method: "DELETE" });
}
