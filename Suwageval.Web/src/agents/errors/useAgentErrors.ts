import { useRequest } from "../../api/useRequest";
import { fetchAgentErrors, type AgentError } from "./api";

/**
 * The agent's open errors, decided over the given versions, asked again whenever they change. Nothing is asked while `agent` is null.
 * A failed request gives an empty list: the card is a hint, never a reason to break the page.
 */
export function useAgentErrors(agent: string | null, versionIds: readonly string[]): AgentError[] {
    const errors = useRequest(
        agent === null ? null : JSON.stringify([agent, versionIds]),
        (signal) => fetchAgentErrors(agent ?? "", versionIds, signal),
        { fallback: NONE },
    );
    return errors.kind === "loaded" && !errors.pending ? errors.value : NONE;
}

const NONE: AgentError[] = [];
