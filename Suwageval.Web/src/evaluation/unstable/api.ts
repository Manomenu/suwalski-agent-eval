// This feature's HTTP calls, typed from the generated OpenAPI types.
import { request } from "../../api/client";
import type { components } from "../../api/openapi";
import { unstableChecksPath } from "./stability";

export type UnstableCheck = components["schemas"]["UnstableCheck"];

/** The agent's unstable checks (see `stability.ts`), limited to the given versions (none: every version not archived). */
export function fetchUnstableChecks(
    agent: string,
    versionIds: readonly string[],
    dataVersions: readonly string[],
    signal: AbortSignal,
): Promise<UnstableCheck[]> {
    return request<UnstableCheck[]>(unstableChecksPath(agent, versionIds, dataVersions), { signal });
}
