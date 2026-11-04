import { request } from "../../api/client";
import type { components } from "../../api/openapi";
import { agentPath } from "../../agents/api";

export type RunGroupSummary = components["schemas"]["RunGroupSummary"];

export function fetchRunGroups(name: string, signal: AbortSignal): Promise<RunGroupSummary[]> {
    return request<RunGroupSummary[]>(`/eval${agentPath(name)}/run-groups`, { signal });
}

export async function archiveRunGroups(name: string, names: string[], archived: boolean): Promise<void> {
    const body: components["schemas"]["ArchiveRunGroupsRequest"] = { names, archived };
    await request<undefined>(`/eval${agentPath(name)}/run-groups/archive`, { method: "POST", body: JSON.stringify(body) });
}

export type RunGroupDeletion = components["schemas"]["RunGroupDeletion"];

export function fetchRunGroupDeletion(name: string, group: string, signal: AbortSignal): Promise<RunGroupDeletion> {
    return request<RunGroupDeletion>(`/eval${agentPath(name)}/run-groups/${encodeURIComponent(group)}/deletion`, { signal });
}

export async function deleteRunGroup(name: string, group: string): Promise<void> {
    await request<undefined>(`/eval${agentPath(name)}/run-groups/${encodeURIComponent(group)}`, { method: "DELETE" });
}
