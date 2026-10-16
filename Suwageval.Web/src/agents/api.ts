// This feature's HTTP calls, typed from the generated OpenAPI types.
import { request } from "../api/client";
import type { components } from "../api/openapi";

export type Agent = components["schemas"]["Agent"];
export type AgentDetail = components["schemas"]["AgentDetail"];
export type AgentVersion = components["schemas"]["AgentVersion"];
export type AgentVersionDetail = components["schemas"]["AgentVersionDetail"];

const agentPath = (name: string) => `/agents/${encodeURIComponent(name)}`;

export function fetchAgents(signal: AbortSignal): Promise<Agent[]> {
    return request<Agent[]>("/agents", { signal });
}

export function fetchAgent(name: string, signal: AbortSignal): Promise<AgentDetail> {
    return request<AgentDetail>(agentPath(name), { signal });
}

export function fetchVersions(name: string, signal: AbortSignal): Promise<AgentVersion[]> {
    return request<AgentVersion[]>(`${agentPath(name)}/versions`, { signal });
}

/** Archives (`archived: true`) or restores versions of the agent; nothing is deleted. */
export async function archiveVersions(name: string, versionIds: string[], archived: boolean): Promise<void> {
    const body: components["schemas"]["ArchiveVersionsRequest"] = { versionIds, archived };
    await request<undefined>(`${agentPath(name)}/versions/archive`, { method: "POST", body: JSON.stringify(body) });
}

export function fetchVersion(name: string, versionId: string, signal: AbortSignal): Promise<AgentVersionDetail> {
    return request<AgentVersionDetail>(`${agentPath(name)}/versions/${versionId}`, { signal });
}
