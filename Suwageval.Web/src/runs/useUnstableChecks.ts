import { useEffect, useState } from "react";

import { fetchUnstableChecks, type UnstableCheck } from "./api";

/**
 * The agent's unstable checks (of the given versions, none: all), asked once per agent and set of versions. Nothing is asked
 * while `agent` is null. A failed request gives an empty list: the marks are a hint, never a reason to break the page.
 */
export function useUnstableChecks(agent: string | null, versionIds: readonly string[]): UnstableCheck[] {
    const [loaded, setLoaded] = useState<{ key: string; list: UnstableCheck[] } | null>(null);
    const key = JSON.stringify([agent, versionIds]);

    useEffect(() => {
        if (agent === null) return;
        // StrictMode runs effects twice in development: abort the first request, ignore its result.
        const controller = new AbortController();
        fetchUnstableChecks(agent, versionIds, controller.signal)
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

const NONE: UnstableCheck[] = [];
