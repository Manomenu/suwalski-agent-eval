// This feature's HTTP calls, typed from the generated OpenAPI types.
import { request } from "../api/client";
import type { components } from "../api/openapi";

export type Agent = components["schemas"]["Agent"];
export type AgentDetail = components["schemas"]["AgentDetail"];
export type AgentVersion = components["schemas"]["AgentVersion"];
export type AgentVersionDetail = components["schemas"]["AgentVersionDetail"];

type ScenarioSummary = components["schemas"]["ScenarioSummary"];

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

export type RunPassRates = components["schemas"]["RunPassRates"];

/** The pass rate of the agent's finished runs, oldest first (the newest 50 at most). */
export function fetchPassRates(name: string, signal: AbortSignal): Promise<RunPassRates[]> {
    return request<RunPassRates[]>(`${agentPath(name)}/pass-rates`, { signal });
}

export function fetchVersion(name: string, versionId: string, signal: AbortSignal): Promise<AgentVersionDetail> {
    return request<AgentVersionDetail>(`${agentPath(name)}/versions/${versionId}`, { signal });
}

export function fetchScenarios(name: string, signal: AbortSignal): Promise<ScenarioSummary[]> {
    return request<ScenarioSummary[]>(`${agentPath(name)}/scenarios`, { signal });
}
