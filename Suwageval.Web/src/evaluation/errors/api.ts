import { dataOf } from "@maniumek/suwfrontkit";
import { queryOptions } from "@tanstack/react-query";

import { api } from "../../api/client";
import type { components } from "../../api/openapi";
import { agentErrorsQuery } from "./agentErrors";

export type AgentError = components["schemas"]["AgentError"];

const agentErrorKeys = {
    list: (name: string, versionIds: readonly string[], dataVersions: readonly string[]) =>
        ["agents", name, "agent-errors", versionIds, dataVersions] as const,
};

function fetchAgentErrors(
    name: string,
    versionIds: readonly string[],
    dataVersions: readonly string[],
    signal: AbortSignal,
): Promise<AgentError[]> {
    return dataOf(
        api.GET("/eval/agents/{agent}/agent-errors", {
            params: { path: { agent: name }, query: agentErrorsQuery(versionIds, dataVersions) },
            signal,
        }),
    );
}

export const agentErrorListQuery = (name: string, versionIds: readonly string[], dataVersions: readonly string[]) =>
    queryOptions({
        queryKey: agentErrorKeys.list(name, versionIds, dataVersions),
        queryFn: ({ signal }) => fetchAgentErrors(name, versionIds, dataVersions, signal),
    });
