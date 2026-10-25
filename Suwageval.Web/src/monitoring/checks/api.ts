// This feature's HTTP calls, typed from the generated OpenAPI types.
import { request } from "../../api/client";
import type { components } from "../../api/openapi";

export type MonitoringCheck = components["schemas"]["MonitoringCheck"];
export type MonitoringTrend = components["schemas"]["MonitoringTrend"];

/** The `?environment=` part of a query, or nothing for all environments. */
function environmentQuery(environment: string | null): string {
    return environment === null ? "" : `environment=${encodeURIComponent(environment)}`;
}

/** The agent's monitoring checks with their pass rates over the last 7 and 30 days, in one environment or all. */
export function fetchMonitoringChecks(agent: string, environment: string | null, signal: AbortSignal): Promise<MonitoringCheck[]> {
    const query = environmentQuery(environment);
    return request<MonitoringCheck[]>(`/monitoring/agents/${encodeURIComponent(agent)}/checks${query === "" ? "" : `?${query}`}`, {
        signal,
    });
}

/** Day by day over the last `days`, in one environment or all: the conversations per environment, each check's verdicts per version. */
export function fetchMonitoringTrend(
    agent: string,
    days: number,
    environment: string | null,
    signal: AbortSignal,
): Promise<MonitoringTrend> {
    const query = environmentQuery(environment);
    return request<MonitoringTrend>(
        `/monitoring/agents/${encodeURIComponent(agent)}/trend?days=${String(days)}${query === "" ? "" : `&${query}`}`,
        { signal },
    );
}
