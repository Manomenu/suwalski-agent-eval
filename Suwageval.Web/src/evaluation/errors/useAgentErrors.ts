import { keepPreviousData, useQuery } from "@tanstack/react-query";

import { agentErrorListQuery, type AgentError } from "./api";

export function useAgentErrors(
    agent: string | null,
    versionIds: readonly string[],
    dataVersions: readonly string[],
): { errors: AgentError[]; arrived: boolean } {
    const query = useQuery({
        ...agentErrorListQuery(agent ?? "", versionIds, dataVersions),
        enabled: agent !== null,
        placeholderData: keepPreviousData,
    });

    return {
        errors: query.isPlaceholderData || query.isError ? NONE : (query.data ?? NONE),
        arrived: agent === null || !query.isPending,
    };
}

const NONE: AgentError[] = [];
