import { useEffect, useState } from "react";

import { fetchAgentErrors, type AgentError } from "./api";

/**
 * The agent's open errors, decided over the given versions, asked again whenever they change. Nothing is asked while `agent` is null.
 * A failed request gives an empty list: the card is a hint, never a reason to break the page.
 */
export function useAgentErrors(agent: string | null, versionIds: readonly string[]): AgentError[] {
    const [loaded, setLoaded] = useState<{ key: string; list: AgentError[] } | null>(null);
    const key = JSON.stringify([agent, versionIds]);

    useEffect(() => {
        if (agent === null) return;
        // StrictMode runs effects twice in development: abort the first request, ignore its result.
        const controller = new AbortController();
        fetchAgentErrors(agent, versionIds, controller.signal)
            .then((list) => {
                if (!controller.signal.aborted) setLoaded({ key, list });
            })
            .catch(() => {
                if (!controller.signal.aborted) setLoaded({ key, list: [] });
            });
        return () => {
            controller.abort();
        };
        // `key` says what is asked for; `versionIds` is a new array on every render.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [key]);

    return loaded?.key === key ? loaded.list : NONE;
}

const NONE: AgentError[] = [];
