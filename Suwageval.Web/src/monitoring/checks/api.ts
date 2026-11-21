import { dataOf } from "@maniumek/suwfrontkit";
import { queryOptions } from "@tanstack/react-query";

import { api } from "../../api/client";
import type { components } from "../../api/openapi";

export type MonitoringCheck = components["schemas"]["MonitoringCheck"];
export type MonitoringTrend = components["schemas"]["MonitoringTrend"];

const environmentParams = (environment: string | null) => (environment === null ? {} : { environment });

function fetchMonitoringChecks(agent: string, environment: string | null, signal: AbortSignal): Promise<MonitoringCheck[]> {
    return dataOf(
        api.GET("/monitoring/agents/{agent}/checks", {
            params: { path: { agent }, query: environmentParams(environment) },
            signal,
        }),
    );
}

function fetchMonitoringTrend(agent: string, days: number, environment: string | null, signal: AbortSignal): Promise<MonitoringTrend> {
    return dataOf(
        api.GET("/monitoring/agents/{agent}/trend", {
            params: { path: { agent }, query: { days, ...environmentParams(environment) } },
            signal,
        }),
    );
}

export const monitoringChecksQuery = (agent: string, environment: string | null) =>
    queryOptions({
        queryKey: ["monitoring", agent, "checks", environment] as const,
        queryFn: ({ signal }) => fetchMonitoringChecks(agent, environment, signal),
    });

export const monitoringTrendQuery = (agent: string, days: number, environment: string | null) =>
    queryOptions({
        queryKey: ["monitoring", agent, "trend", days, environment] as const,
        queryFn: ({ signal }) => fetchMonitoringTrend(agent, days, environment, signal),
    });
