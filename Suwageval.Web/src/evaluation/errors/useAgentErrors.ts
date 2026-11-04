import { useRequest } from "../../api/useRequest";
import { fetchAgentErrors, type AgentError } from "./api";

export function useAgentErrors(agent: string | null, versionIds: readonly string[], dataVersions: readonly string[]): AgentError[] {
    const errors = useRequest(
        agent === null ? null : JSON.stringify([agent, versionIds, dataVersions]),
        (signal) => fetchAgentErrors(agent ?? "", versionIds, dataVersions, signal),
        { fallback: NONE },
    );
    return errors.kind === "loaded" && !errors.pending ? errors.value : NONE;
}

const NONE: AgentError[] = [];
