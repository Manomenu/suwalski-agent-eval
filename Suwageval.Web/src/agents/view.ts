// What the agent pages show, as plain functions: links, labels and the JSON of a schema.
import { ApiError } from "../api/client";

export const agentHref = (name: string) => `/agents/${encodeURIComponent(name)}`;
export const versionHref = (name: string, versionId: string) => `${agentHref(name)}/versions/${versionId}`;
export const runHref = (runId: string) => `/runs/${runId}`;

/** The server's 404 — the only error the pages tell apart. */
export function isNotFound(error: Error): boolean {
    return error instanceof ApiError && error.status === 404;
}

/** A version's label, or a dash for one registered without it. */
export function versionLabel(version: { label?: string | null }): string {
    return version.label?.trim() ? version.label : "—";
}

/** A JSON value (a schema, model parameters) as indented text for a code block. */
export function jsonText(value: unknown): string {
    return JSON.stringify(value, null, 2);
}

/** The first eight characters of a guid — enough to tell runs and versions apart in a table. */
/** What names a version in its page's title: its label, or the start of its id when it has none. */
export function versionTitle(id: string, label: string | null | undefined): string {
    return label?.trim() ? label : shortId(id);
}

export function shortId(id: string): string {
    return id.slice(0, 8);
}
