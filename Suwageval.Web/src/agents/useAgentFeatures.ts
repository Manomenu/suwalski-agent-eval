import { useRequest } from "../api/useRequest";
import { fetchAgent, type AgentDetail } from "./api";

export function useAgentFeatures(name: string | null): AgentDetail["features"] | null {
    const agent = useRequest(name, (signal) => fetchAgent(name ?? "", signal));
    return agent.kind === "loaded" ? agent.value.features : null;
}
