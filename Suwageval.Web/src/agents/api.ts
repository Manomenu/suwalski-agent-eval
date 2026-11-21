import { dataOf } from "@maniumek/suwfrontkit";
import { queryOptions } from "@tanstack/react-query";

import { api } from "../api/client";
import type { components } from "../api/openapi";

export type Agent = components["schemas"]["Agent"];
export type AgentDetail = components["schemas"]["AgentDetail"];
export type AgentVersion = components["schemas"]["AgentVersion"];
export type AgentVersionDetail = components["schemas"]["AgentVersionDetail"];
export type TagUsage = components["schemas"]["TagUsage"];
export type AgentDeletion = components["schemas"]["AgentDeletion"];

export const agentKeys = {
    all: ["agents"] as const,
    agent: (name: string) => ["agents", name] as const,
    versions: (name: string) => ["agents", name, "versions"] as const,
    version: (name: string, versionId: string) => ["agents", name, "versions", versionId] as const,
    tags: (name: string) => ["agents", name, "tags"] as const,
    deletion: (name: string) => ["agents", name, "deletion"] as const,
};

function fetchAgents(signal: AbortSignal): Promise<Agent[]> {
    return dataOf(api.GET("/agents", { signal }));
}

function fetchAgent(name: string, signal: AbortSignal): Promise<AgentDetail> {
    return dataOf(api.GET("/agents/{agent}", { params: { path: { agent: name } }, signal }));
}

function fetchVersions(name: string, signal: AbortSignal): Promise<AgentVersion[]> {
    return dataOf(api.GET("/agents/{agent}/versions", { params: { path: { agent: name } }, signal }));
}

function fetchTags(name: string, signal: AbortSignal): Promise<TagUsage[]> {
    return dataOf(api.GET("/eval/agents/{agent}/tags", { params: { path: { agent: name } }, signal }));
}

export function fetchAgentDeletion(name: string, signal: AbortSignal): Promise<AgentDeletion> {
    return dataOf(api.GET("/agents/{agent}/deletion", { params: { path: { agent: name } }, signal }));
}

export async function deleteAgent(name: string): Promise<void> {
    await dataOf(api.DELETE("/agents/{agent}", { params: { path: { agent: name } } }));
}

export function fetchVersion(name: string, versionId: string, signal: AbortSignal): Promise<AgentVersionDetail> {
    return dataOf(api.GET("/agents/{agent}/versions/{versionId}", { params: { path: { agent: name, versionId } }, signal }));
}

export const agentsQuery = () => queryOptions({ queryKey: agentKeys.all, queryFn: ({ signal }) => fetchAgents(signal) });

export const agentQuery = (name: string) =>
    queryOptions({ queryKey: agentKeys.agent(name), queryFn: ({ signal }) => fetchAgent(name, signal) });

export const versionsQuery = (name: string) =>
    queryOptions({ queryKey: agentKeys.versions(name), queryFn: ({ signal }) => fetchVersions(name, signal) });

export const versionQuery = (name: string, versionId: string) =>
    queryOptions({ queryKey: agentKeys.version(name, versionId), queryFn: ({ signal }) => fetchVersion(name, versionId, signal) });

export const tagsQuery = (name: string) =>
    queryOptions({ queryKey: agentKeys.tags(name), queryFn: ({ signal }) => fetchTags(name, signal) });
