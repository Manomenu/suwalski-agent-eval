import { dataOf } from "@maniumek/suwfrontkit";
import { queryOptions } from "@tanstack/react-query";

import { api } from "../../api/client";
import type { components } from "../../api/openapi";
import { unstableChecksQuery } from "./stability";

export type UnstableCheck = components["schemas"]["UnstableCheck"];

const unstableKeys = {
    list: (name: string, versionIds: readonly string[], eachVersion: boolean, dataVersions: readonly string[]) =>
        ["agents", name, "unstable-checks", versionIds, eachVersion, dataVersions] as const,
};

function fetchUnstableChecks(
    agent: string,
    versionIds: readonly string[],
    dataVersions: readonly string[],
    signal: AbortSignal,
): Promise<UnstableCheck[]> {
    return dataOf(
        api.GET("/eval/agents/{agent}/unstable-checks", {
            params: { path: { agent }, query: unstableChecksQuery(versionIds, dataVersions) },
            signal,
        }),
    );
}

export const unstableListQuery = (name: string, versionIds: readonly string[], eachVersion: boolean, dataVersions: readonly string[]) =>
    queryOptions({
        queryKey: unstableKeys.list(name, versionIds, eachVersion, dataVersions),
        queryFn: ({ signal }) =>
            eachVersion
                ? Promise.all(versionIds.map((id) => fetchUnstableChecks(name, [id], dataVersions, signal))).then((lists) => lists.flat())
                : fetchUnstableChecks(name, versionIds, dataVersions, signal),
    });
