// The addresses of the pages, built in one place (`pageOf` reads them back): a page links to another by these, never by a path of its own.

/** The page of an agent. */
export const agentHref = (name: string) => `/agents/${encodeURIComponent(name)}`;

/** The page of one version of an agent. */
export const versionHref = (name: string, versionId: string) => `${agentHref(name)}/versions/${versionId}`;

/** The page showing what changed between two versions of the agent. */
export const versionCompareHref = (name: string, a: string, b: string) => `${agentHref(name)}/versions/compare?a=${a}&b=${b}`;

/** The page of a run. */
export const runHref = (runId: string) => `/runs/${runId}`;

/** The comparison page takes at least two runs and at most twenty (the server's limit). */
export const COMPARE_MIN_RUNS = 2;
export const COMPARE_MAX_RUNS = 20;

/** The compare page for these runs, side by side; with a `baseline` (one of them) the others are read against it. */
export const compareHref = (runIds: readonly string[], baseline?: string) =>
    `/compare?runs=${runIds.join(",")}${baseline === undefined ? "" : `&baseline=${baseline}`}`;

/** The compare page of the runs of a group; `agent` says whose, for a group name used by several agents. */
export const compareGroupHref = (group: string, agent: string) => `/compare?${new URLSearchParams({ group, agent }).toString()}`;

/** The id of a scenario's card on the run page, so a link can jump to it (`#scenario-<key>`). */
export function scenarioAnchor(scenarioKey: string): string {
    return `scenario-${scenarioKey}`;
}

/** The scenario's card on the run page: where the comparison's "open in run" leads. */
export const scenarioInRunHref = (runId: string, scenarioKey: string) => `${runHref(runId)}#${scenarioAnchor(scenarioKey)}`;
