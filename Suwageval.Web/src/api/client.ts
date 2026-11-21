import { ApiError, createApi } from "@maniumek/suwfrontkit";

import type { paths } from "./openapi";

export const API_BASE = import.meta.env.VITE_API_BASE ?? "/api";

export const api = createApi<paths>({ baseUrl: API_BASE });

export function isNotFound(error: unknown): boolean {
    return error instanceof ApiError && error.status === 404;
}
