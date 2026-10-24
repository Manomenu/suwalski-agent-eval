// The addresses of the pages, built in one place (`pageOf` reads them back): a page links to another by these, never by a path of its own.

/** The page of an agent: its Versions, which belong to no area. */
export const agentHref = (name: string) => `/agents/${encodeURIComponent(name)}`;

/** The Evaluation page of an agent: its runs, groups, trends and problems (the agent's runs: where a link goes that means them). */
export const agentEvaluationHref = (name: string) => `${agentHref(name)}/evaluation`;

/** The Monitoring page of an agent: the conversations of its deployed agent and the checks run on them. */
export const agentMonitoringHref = (name: string) => `${agentHref(name)}/monitoring`;

/** The Monitoring page with its conversations narrowed to those that failed a monitoring check (`failedCheck`, what the table reads). */
export const failedConversationsHref = (name: string, checkKey: string) =>
    `${agentMonitoringHref(name)}?${new URLSearchParams({ failedCheck: checkKey }).toString()}#conversations`;

/** The Versions page of an agent: its versions, to archive, restore, delete and compare (what `agentHref` opens too). */
export const agentVersionsHref = (name: string) => `${agentHref(name)}/versions`;

/** Where a version is used: the kinds of usage the Versions page filters by (the closed set; a later area adds its own). */
export type VersionUsage = "evaluation" | "groups" | "monitoring" | "unused" | "archived";

/**
 * The Versions page of an agent filtered by usage, and by version when ids are given (`?usage=evaluation,groups&version=<id>,<id>`). The
 * page reads them back (`versions/versionUsage.ts`); an address without them is its default.
 */
export function agentVersionsFilteredHref(name: string, usage: readonly VersionUsage[], versionIds: readonly string[] = []): string {
    const query = new URLSearchParams({ usage: usage.join(",") });
    if (versionIds.length > 0) query.set("version", versionIds.join(","));
    return `${agentVersionsHref(name)}?${query.toString().replaceAll("%2C", ",")}`;
}

/** The versions in evaluation: the Evaluation page's "Versions in evaluation" link. */
export const versionsInEvaluationHref = (name: string) => agentVersionsFilteredHref(name, ["evaluation"]);

/** The versions answering the deployed agent's conversations: the Monitoring page's "Versions in monitoring" link. */
export const versionsInMonitoringHref = (name: string) => agentVersionsFilteredHref(name, ["monitoring"]);

/** The Versions list narrowed to one version a monitored conversation points at. */
export const versionInMonitoringHref = (name: string, versionId: string) => agentVersionsFilteredHref(name, ["monitoring"], [versionId]);

/**
 * The Versions list narrowed to one version a run of the Evaluation area points at: with the evaluation usage, and the groups' too when the
 * run is in a group (a version with only group runs would not show otherwise).
 */
export const versionInUseHref = (name: string, versionId: string, inGroup: boolean) =>
    agentVersionsFilteredHref(name, inGroup ? ["evaluation", "groups"] : ["evaluation"], [versionId]);

/** The page of one version of an agent. */
export const versionHref = (name: string, versionId: string) => `${agentHref(name)}/versions/${versionId}`;

/** The page showing what changed between two versions of the agent. */
export const versionCompareHref = (name: string, a: string, b: string) => `${agentHref(name)}/versions/compare?a=${a}&b=${b}`;

/** The page of a run. */
export const runHref = (runId: string) => `/runs/${runId}`;

/** The page of one monitored conversation, with its turns. */
export const conversationHref = (id: string) => `/conversations/${id}`;

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
