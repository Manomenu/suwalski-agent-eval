import { request } from "../api/client";
import type { components } from "../api/openapi";

export type Health = components["schemas"]["Health"];

export function fetchHealth(signal: AbortSignal): Promise<Health> {
    return request<Health>("/health", { signal });
}
