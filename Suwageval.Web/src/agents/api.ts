// This feature's HTTP calls, typed from the generated OpenAPI types.
import { request } from "../api/client";
import type { components } from "../api/openapi";
import { agentErrorsPath } from "./agentErrors";

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

export type VersionDeletion = components["schemas"]["VersionDeletion"];

/** What deleting the version would remove; the server refuses like the deletion would (409: not archived, or a run in progress). */
export function fetchVersionDeletion(name: string, versionId: string, signal: AbortSignal): Promise<VersionDeletion> {
    return request<VersionDeletion>(`${agentPath(name)}/versions/${versionId}/deletion`, { signal });
}

/** Deletes an archived version with its runs, samples and verdicts. */
export async function deleteVersion(name: string, versionId: string): Promise<void> {
    await request<undefined>(`${agentPath(name)}/versions/${versionId}`, { method: "DELETE" });
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

export type RunGroupSummary = components["schemas"]["RunGroupSummary"];

/** The groups of the agent's runs (run --group), the one with the newest run first. */
export function fetchRunGroups(name: string, signal: AbortSignal): Promise<RunGroupSummary[]> {
    return request<RunGroupSummary[]>(`${agentPath(name)}/run-groups`, { signal });
}

export type RunPassRates = components["schemas"]["RunPassRates"];

/** The pass rate of the agent's finished runs, oldest first (the newest 50 at most). */
export function fetchPassRates(name: string, signal: AbortSignal): Promise<RunPassRates[]> {
    return request<RunPassRates[]>(`${agentPath(name)}/pass-rates`, { signal });
}

export type AgentError = components["schemas"]["AgentError"];

/** The open scenarios where the agent gave no answer, per version, worst first (see `agentErrors.ts`), decided over the given versions. */
export function fetchAgentErrors(name: string, versionIds: readonly string[], signal: AbortSignal): Promise<AgentError[]> {
    return request<AgentError[]>(agentErrorsPath(name, versionIds), { signal });
}

export function fetchVersion(name: string, versionId: string, signal: AbortSignal): Promise<AgentVersionDetail> {
    return request<AgentVersionDetail>(`${agentPath(name)}/versions/${versionId}`, { signal });
}

export type VersionDiff = components["schemas"]["VersionDiff"];

/** What changed between two versions, the older first: both contracts as YAML compared by lines, computed by the server. */
export function fetchVersionDiff(name: string, from: string, to: string, signal: AbortSignal): Promise<VersionDiff> {
    const query = new URLSearchParams({ from, to });
    return request<VersionDiff>(`${agentPath(name)}/versions/diff?${query.toString()}`, { signal });
}
