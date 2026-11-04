import { useRequest } from "../../api/useRequest";
import { fetchAgentErrors, type AgentError } from "./api";

export function useAgentErrors(
    agent: string | null,
    versionIds: readonly string[],
    dataVersions: readonly string[],
): { errors: AgentError[]; arrived: boolean } {
    const request = useRequest(
        agent === null ? null : JSON.stringify([agent, versionIds, dataVersions]),
        (signal) => fetchAgentErrors(agent ?? "", versionIds, dataVersions, signal),
        { fallback: NONE },
    );
    // Why: arrived tells the page the first answer is in (or none is asked for), so it can hold the view back instead of letting the cards push the filters down later
    return {
        errors: request.kind === "loaded" && !request.pending ? request.value : NONE,
        arrived: request.kind !== "loading" || !request.pending,
    };
}

const NONE: AgentError[] = [];
