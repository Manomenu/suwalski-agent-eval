import { keepPreviousData, useQuery } from "@tanstack/react-query";

import { unstableListQuery, type UnstableCheck } from "./api";

export function useUnstableChecks(
    agent: string | null,
    versionIds: readonly string[],
    eachVersion = false,
    dataVersions: readonly string[] = NO_DATA,
): { checks: UnstableCheck[]; arrived: boolean } {
    const query = useQuery({
        ...unstableListQuery(agent ?? "", versionIds, eachVersion, dataVersions),
        enabled: agent !== null,
        placeholderData: keepPreviousData,
    });

    return {
        checks: query.isPlaceholderData || query.isError ? NONE : (query.data ?? NONE),
        arrived: agent === null || !query.isPending,
    };
}

const NONE: UnstableCheck[] = [];
const NO_DATA: readonly string[] = [];
