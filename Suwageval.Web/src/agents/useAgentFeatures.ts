import { useQuery } from "@tanstack/react-query";

import { agentQuery, type AgentDetail } from "./api";

export function useAgentFeatures(name: string | null): AgentDetail["features"] | null {
    const agent = useQuery({ ...agentQuery(name ?? ""), enabled: name !== null });
    return agent.data?.features ?? null;
}
