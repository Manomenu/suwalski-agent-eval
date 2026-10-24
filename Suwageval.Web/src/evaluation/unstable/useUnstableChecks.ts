import { useRequest } from "../../api/useRequest";
import { fetchUnstableChecks, type UnstableCheck } from "./api";

/**
 * The agent's unstable checks (decided over the given versions, none: every version not archived; and over the runs on the given data
 * versions, none: any), asked once per agent and set of versions. Nothing is asked
 * while `agent` is null. A failed request gives an empty list: the marks are a hint, never a reason to break the page.
 * With `eachVersion`, every version is asked on its own, so a newer one does not close what an older one shows (the comparison
 * of runs marks each run by its own version).
 */
export function useUnstableChecks(
    agent: string | null,
    versionIds: readonly string[],
    eachVersion = false,
    dataVersions: readonly string[] = NO_DATA,
): UnstableCheck[] {
    const checks = useRequest(
        agent === null ? null : JSON.stringify([agent, versionIds, eachVersion, dataVersions]),
        (signal) =>
            eachVersion
                ? Promise.all(versionIds.map((id) => fetchUnstableChecks(agent ?? "", [id], dataVersions, signal))).then((lists) =>
                      lists.flat(),
                  )
                : fetchUnstableChecks(agent ?? "", versionIds, dataVersions, signal),
        { fallback: NONE },
    );
    return checks.kind === "loaded" && !checks.pending ? checks.value : NONE;
}

const NONE: UnstableCheck[] = [];
const NO_DATA: readonly string[] = [];
