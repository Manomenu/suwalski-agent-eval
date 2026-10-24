// This feature's HTTP calls, typed from the generated OpenAPI types.
import { request } from "../../api/client";
import type { components } from "../../api/openapi";

export type MonitoringCheck = components["schemas"]["MonitoringCheck"];
export type MonitoringTrend = components["schemas"]["MonitoringTrend"];

/** The agent's monitoring checks with their pass rates over the last 7 and 30 days. */
export function fetchMonitoringChecks(agent: string, signal: AbortSignal): Promise<MonitoringCheck[]> {
    return request<MonitoringCheck[]>(`/monitoring/agents/${encodeURIComponent(agent)}/checks`, { signal });
}

/** Day by day over the last `days`: the conversations, how many were checked, each check's verdicts per version. */
export function fetchMonitoringTrend(agent: string, days: number, signal: AbortSignal): Promise<MonitoringTrend> {
    return request<MonitoringTrend>(`/monitoring/agents/${encodeURIComponent(agent)}/trend?days=${String(days)}`, { signal });
}
