import { request } from "../../api/client";
import type { components } from "../../api/openapi";
import { unstableChecksPath } from "./stability";

export type UnstableCheck = components["schemas"]["UnstableCheck"];

export function fetchUnstableChecks(
    agent: string,
    versionIds: readonly string[],
    dataVersions: readonly string[],
    signal: AbortSignal,
): Promise<UnstableCheck[]> {
    return request<UnstableCheck[]>(unstableChecksPath(agent, versionIds, dataVersions), { signal });
}
