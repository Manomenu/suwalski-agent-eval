import { ApiError, createApi, refusalReader } from "@maniumek/suwfrontkit";

import type { components, paths } from "./openapi";

export const API_BASE = import.meta.env.VITE_API_BASE ?? "/api";

export const api = createApi<paths>({ baseUrl: API_BASE });

export const { mistakesOf } = refusalReader<components["schemas"]["RefusalCode"], components["schemas"]["MistakeCode"]>();

export function isNotFound(error: unknown): boolean {
    return error instanceof ApiError && error.status === 404;
}

export function headlineOf(error: unknown): string {
    if (!(error instanceof Error)) return String(error);
    const title = error instanceof ApiError && mistakesOf(error).length > 0 ? error.problem["title"] : undefined;
    return typeof title === "string" && title !== "" ? title : error.message;
}

export function messageOf(error: unknown): string {
    const mistakes = mistakesOf(error);
    if (mistakes.length === 0) return headlineOf(error);
    return mistakes.map((mistake) => `${mistake.message} ${mistake.fix}`).join(" ");
}
