// This feature's HTTP calls, typed from the generated OpenAPI types.
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

/** The tags of the agent's scenarios, with how many scenarios carry each. */
export function fetchTags(name: string, signal: AbortSignal): Promise<TagUsage[]> {
    return request<TagUsage[]>(`/eval${agentPath(name)}/tags`, { signal });
}

export type AgentDeletion = components["schemas"]["AgentDeletion"];

/** What deleting the agent would remove; the server refuses like the deletion would (409: a run in progress). */
export function fetchAgentDeletion(name: string, signal: AbortSignal): Promise<AgentDeletion> {
    return request<AgentDeletion>(`${agentPath(name)}/deletion`, { signal });
}

/** Deletes the agent with its versions, scenarios, checks, runs, samples and verdicts. */
export async function deleteAgent(name: string): Promise<void> {
    await request<undefined>(agentPath(name), { method: "DELETE" });
}

export function fetchVersion(name: string, versionId: string, signal: AbortSignal): Promise<AgentVersionDetail> {
    return request<AgentVersionDetail>(`${agentPath(name)}/versions/${versionId}`, { signal });
}
