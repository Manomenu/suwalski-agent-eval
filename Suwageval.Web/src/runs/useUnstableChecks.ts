import { useEffect, useState } from "react";

import { fetchUnstableChecks, type UnstableCheck } from "./api";

/**
 * The agent's unstable checks (decided over the given versions, none: every version not archived), asked once per agent and set of versions. Nothing is asked
 * while `agent` is null. A failed request gives an empty list: the marks are a hint, never a reason to break the page.
 * With `eachVersion`, every version is asked on its own, so a newer one does not close what an older one shows (the comparison
 * of runs marks each run by its own version).
 */
export function useUnstableChecks(agent: string | null, versionIds: readonly string[], eachVersion = false): UnstableCheck[] {
    const [loaded, setLoaded] = useState<{ key: string; list: UnstableCheck[] } | null>(null);
    const key = JSON.stringify([agent, versionIds, eachVersion]);

    useEffect(() => {
        if (agent === null) return;
        // StrictMode runs effects twice in development: abort the first request, ignore its result.
        const controller = new AbortController();
        const asked = eachVersion
            ? Promise.all(versionIds.map((id) => fetchUnstableChecks(agent, [id], controller.signal))).then((lists) => lists.flat())
            : fetchUnstableChecks(agent, versionIds, controller.signal);
        asked
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
