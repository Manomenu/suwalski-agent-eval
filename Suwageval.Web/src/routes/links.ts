export const agentHref = (name: string) => `/agents/${encodeURIComponent(name)}`;

export const agentEvaluationHref = (name: string) => `${agentHref(name)}/evaluation`;

export const agentMonitoringHref = (name: string) => `${agentHref(name)}/monitoring`;

export const failedConversationsHref = (name: string, checkKey: string, environment: string | null = null) =>
    `${agentMonitoringHref(name)}?${new URLSearchParams({ failedCheck: checkKey, ...(environment === null ? {} : { environment }) }).toString()}#conversations`;

export const agentVersionsHref = (name: string) => `${agentHref(name)}/versions`;

export type VersionUsage = "evaluation" | "groups" | "monitoring" | "unused" | "archived";

export function agentVersionsFilteredHref(name: string, usage: readonly VersionUsage[], versionIds: readonly string[] = []): string {
    const query = new URLSearchParams({ usage: usage.join(",") });
    if (versionIds.length > 0) query.set("version", versionIds.join(","));
    return `${agentVersionsHref(name)}?${query.toString().replaceAll("%2C", ",")}`;
}

export const versionsInEvaluationHref = (name: string) => agentVersionsFilteredHref(name, ["evaluation"]);

export const versionsInMonitoringHref = (name: string) => agentVersionsFilteredHref(name, ["monitoring"]);

export const versionInMonitoringHref = (name: string, versionId: string) => agentVersionsFilteredHref(name, ["monitoring"], [versionId]);

export const versionInUseHref = (name: string, versionId: string, inGroup: boolean) =>
    agentVersionsFilteredHref(name, inGroup ? ["evaluation", "groups"] : ["evaluation"], [versionId]);

export const versionHref = (name: string, versionId: string) => `${agentHref(name)}/versions/${versionId}`;

export const versionCompareHref = (name: string, a: string, b: string) => `${agentHref(name)}/versions/compare?a=${a}&b=${b}`;

export const runHref = (runId: string) => `/runs/${runId}`;

export const conversationHref = (id: string) => `/conversations/${id}`;

export const COMPARE_MIN_RUNS = 2;
export const COMPARE_MAX_RUNS = 7;

export const compareHref = (runIds: readonly string[], baseline?: string) =>
    `/compare?runs=${runIds.join(",")}${baseline === undefined ? "" : `&baseline=${baseline}`}`;

export const compareGroupHref = (group: string, agent: string) => `/compare?${new URLSearchParams({ group, agent }).toString()}`;

export function scenarioAnchor(scenarioKey: string): string {
    return `scenario-${scenarioKey}`;
}

export const scenarioInRunHref = (runId: string, scenarioKey: string) => `${runHref(runId)}#${scenarioAnchor(scenarioKey)}`;
