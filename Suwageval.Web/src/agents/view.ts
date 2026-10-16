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

/** The versions to list: all of them with "Show archived" on, otherwise the active ones. */
export function visibleVersions<T extends { archivedAt: string | null }>(versions: T[], showArchived: boolean): T[] {
    return showArchived ? versions : versions.filter((version) => version.archivedAt === null);
}

/** The runs to list: without "Show archived", those of an archived version are hidden (a run of an unknown version stays). */
export function visibleRuns<T extends { agentVersionId: string }>(
    runs: T[],
    versions: { id: string; archivedAt: string | null }[],
    showArchived: boolean,
): T[] {
    if (showArchived) return runs;
    const archived = new Set(versions.filter((version) => version.archivedAt !== null).map((version) => version.id));
    return runs.filter((run) => !archived.has(run.agentVersionId));
}

/** What the selected versions need: the active ones can be archived, the archived ones restored. */
export function selectionActions(
    versions: { id: string; archivedAt: string | null }[],
    selected: ReadonlySet<string>,
): { toArchive: string[]; toRestore: string[] } {
    const chosen = versions.filter((version) => selected.has(version.id));
    return {
        toArchive: chosen.filter((version) => version.archivedAt === null).map((version) => version.id),
        toRestore: chosen.filter((version) => version.archivedAt !== null).map((version) => version.id),
    };
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
