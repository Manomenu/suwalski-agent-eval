import { useRequest } from "../../api/useRequest";
import { fetchUnstableChecks, type UnstableCheck } from "./api";

export function useUnstableChecks(
    agent: string | null,
    versionIds: readonly string[],
    eachVersion = false,
    dataVersions: readonly string[] = NO_DATA,
): { checks: UnstableCheck[]; arrived: boolean } {
    const request = useRequest(
        agent === null ? null : JSON.stringify([agent, versionIds, eachVersion, dataVersions]),
        (signal) =>
            eachVersion
                ? Promise.all(versionIds.map((id) => fetchUnstableChecks(agent ?? "", [id], dataVersions, signal))).then((lists) =>
                      lists.flat(),
                  )
                : fetchUnstableChecks(agent ?? "", versionIds, dataVersions, signal),
        { fallback: NONE },
    );

    return {
        checks: request.kind === "loaded" && !request.pending ? request.value : NONE,
        arrived: request.kind !== "loading" || !request.pending,
    };
}

const NONE: UnstableCheck[] = [];
const NO_DATA: readonly string[] = [];
