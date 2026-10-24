import { useRequest } from "../api/useRequest";
import { fetchAgent, type AgentDetail } from "./api";

/** The areas the agent uses, once known; null while loading, when the agent cannot be read or when there is no agent. */
export function useAgentFeatures(name: string | null): AgentDetail["features"] | null {
    const agent = useRequest(name, (signal) => fetchAgent(name ?? "", signal));
    return agent.kind === "loaded" ? agent.value.features : null;
}
