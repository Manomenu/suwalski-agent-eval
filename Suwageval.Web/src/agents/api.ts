import { request } from "../api/client";
import type { components } from "../api/openapi";

export type Agent = components["schemas"]["Agent"];
export type AgentDetail = components["schemas"]["AgentDetail"];
export type AgentVersion = components["schemas"]["AgentVersion"];
export type AgentVersionDetail = components["schemas"]["AgentVersionDetail"];

export const agentPath = (name: string) => `/agents/${encodeURIComponent(name)}`;

export function fetchAgents(signal: AbortSignal): Promise<Agent[]> {
    return request<Agent[]>("/agents", { signal });
}

export function fetchAgent(name: string, signal: AbortSignal): Promise<AgentDetail> {
    return request<AgentDetail>(agentPath(name), { signal });
}

export function fetchVersions(name: string, signal: AbortSignal): Promise<AgentVersion[]> {
    return request<AgentVersion[]>(`${agentPath(name)}/versions`, { signal });
}

export type TagUsage = components["schemas"]["TagUsage"];

export function fetchTags(name: string, signal: AbortSignal): Promise<TagUsage[]> {
    return request<TagUsage[]>(`/eval${agentPath(name)}/tags`, { signal });
}

export type AgentDeletion = components["schemas"]["AgentDeletion"];

export function fetchAgentDeletion(name: string, signal: AbortSignal): Promise<AgentDeletion> {
    return request<AgentDeletion>(`${agentPath(name)}/deletion`, { signal });
}

export async function deleteAgent(name: string): Promise<void> {
    await request<undefined>(agentPath(name), { method: "DELETE" });
}

export function fetchVersion(name: string, versionId: string, signal: AbortSignal): Promise<AgentVersionDetail> {
    return request<AgentVersionDetail>(`${agentPath(name)}/versions/${versionId}`, { signal });
}
