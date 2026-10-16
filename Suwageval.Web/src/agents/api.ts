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

export function fetchVersion(name: string, versionId: string, signal: AbortSignal): Promise<AgentVersionDetail> {
    return request<AgentVersionDetail>(`${agentPath(name)}/versions/${versionId}`, { signal });
}
