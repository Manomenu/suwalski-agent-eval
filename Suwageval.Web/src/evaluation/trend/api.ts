import { dataOf } from "@maniumek/suwfrontkit";
import { queryOptions } from "@tanstack/react-query";

import { api } from "../../api/client";
import type { components } from "../../api/openapi";

export type RunPassRates = components["schemas"]["RunPassRates"];

const passRateKeys = { list: (name: string) => ["agents", name, "pass-rates"] as const };

function fetchPassRates(name: string, signal: AbortSignal): Promise<RunPassRates[]> {
    return dataOf(api.GET("/eval/agents/{agent}/pass-rates", { params: { path: { agent: name } }, signal }));
}

export const passRatesQuery = (name: string) =>
    queryOptions({ queryKey: passRateKeys.list(name), queryFn: ({ signal }) => fetchPassRates(name, signal) });
