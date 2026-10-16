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

/** The models (the contract's `model.name`) of the versions, sorted, each once — what the model filter offers. */
export function modelsOf(versions: { modelName: string }[]): string[] {
    return [...new Set(versions.map((version) => version.modelName))].sort((a, b) => a.localeCompare(b));
}

/**
 * The runs to list. Without "Show archived", those of an archived version are hidden (a run of an unknown version stays).
 * With models chosen, only the runs of a version of one of those models stay (an unknown version has no model, so it goes);
 * no model chosen means all of them.
 */
export function visibleRuns<T extends { agentVersionId: string }>(
    runs: T[],
    versions: { id: string; archivedAt: string | null; modelName: string }[],
    showArchived: boolean,
    models: readonly string[] = [],
): T[] {
    const archived = new Set(versions.filter((version) => version.archivedAt !== null).map((version) => version.id));
    const chosen = new Set(models);
    const modelOf = new Map(versions.map((version) => [version.id, version.modelName]));
    return runs.filter((run) => {
        if (!showArchived && archived.has(run.agentVersionId)) return false;
        if (chosen.size === 0) return true;
        const model = modelOf.get(run.agentVersionId);
        return model !== undefined && chosen.has(model);
    });
}

/** Comparing takes at least two runs and at most five (more would not fit a screen). */
export const COMPARE_MIN_RUNS = 2;
export const COMPARE_MAX_RUNS = 5;

/** The compare page for these runs, the first being the baseline. */
export const compareHref = (runIds: readonly string[]) => `/compare?runs=${runIds.join(",")}`;

/** Whether the selection is a number of runs the compare page accepts. */
export function canCompare(count: number): boolean {
    return count >= COMPARE_MIN_RUNS && count <= COMPARE_MAX_RUNS;
}

/** The selected runs oldest first (the oldest is the baseline). `runs` is as the list has it, newest first. */
export function runsToCompare(runs: { id: string }[], selected: ReadonlySet<string>): string[] {
    return runs
        .filter((run) => selected.has(run.id))
        .map((run) => run.id)
        .reverse();
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
