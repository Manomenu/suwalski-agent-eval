import { request } from "../../api/client";
import type { components } from "../../api/openapi";

export type MonitoringCheck = components["schemas"]["MonitoringCheck"];
export type MonitoringTrend = components["schemas"]["MonitoringTrend"];

function environmentQuery(environment: string | null): string {
    return environment === null ? "" : `environment=${encodeURIComponent(environment)}`;
}

export function fetchMonitoringChecks(agent: string, environment: string | null, signal: AbortSignal): Promise<MonitoringCheck[]> {
    const query = environmentQuery(environment);
    return request<MonitoringCheck[]>(`/monitoring/agents/${encodeURIComponent(agent)}/checks${query === "" ? "" : `?${query}`}`, {
        signal,
    });
}

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
