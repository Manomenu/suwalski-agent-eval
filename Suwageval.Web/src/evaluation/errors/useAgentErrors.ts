import { useRequest } from "../../api/useRequest";
import { fetchAgentErrors, type AgentError } from "./api";

/**
 * The agent's open errors, decided over the given versions and the runs on the given data versions (none: any), asked again whenever they change. Nothing is asked while `agent` is null.
 * A failed request gives an empty list: the card is a hint, never a reason to break the page.
 */
export function useAgentErrors(agent: string | null, versionIds: readonly string[], dataVersions: readonly string[]): AgentError[] {
    const errors = useRequest(
        agent === null ? null : JSON.stringify([agent, versionIds, dataVersions]),
        (signal) => fetchAgentErrors(agent ?? "", versionIds, dataVersions, signal),
        { fallback: NONE },
    );
    return errors.kind === "loaded" && !errors.pending ? errors.value : NONE;
}

const NONE: AgentError[] = [];
