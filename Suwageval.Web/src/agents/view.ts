// What the agent pages show, as plain functions: links, labels and the JSON of a schema.
import { PAGE_SIZE } from "../runs/paging";

export const agentHref = (name: string) => `/agents/${encodeURIComponent(name)}`;
export const versionHref = (name: string, versionId: string) => `${agentHref(name)}/versions/${versionId}`;

/** A version's label, or a dash for one registered without it. */
export function versionLabel(version: { label?: string | null }): string {
    return version.label?.trim() ? version.label : "—";
}

/** The versions to list: all of them with "Show archived" on, otherwise the active ones. */
export function visibleVersions<T extends { archivedAt?: string | null }>(versions: T[], showArchived: boolean): T[] {
    return showArchived ? versions : versions.filter((version) => version.archivedAt == null);
}

/** The models (the `model.name` of every LLM agent) of the versions, sorted, each once — what the model filter offers. */
export function modelsOf(versions: { modelNames: string[] }[]): string[] {
    return [...new Set(versions.flatMap((version) => version.modelNames))].sort((a, b) => a.localeCompare(b));
}

/** The data versions of the versions that have retrieval, sorted, each once — what the data version filter offers. */
export function dataVersionsOf(versions: { dataVersion?: string | null }[]): string[] {
    return [...new Set(versions.flatMap((version) => (version.dataVersion == null ? [] : [version.dataVersion])))].sort((a, b) =>
        a.localeCompare(b),
    );
}

/** "data build-7 · 1832 articles": the version of an agent's retrieval data and what it is; null for an agent without retrieval. */
export function dataText(version: { dataVersion?: string | null; dataDescription?: string | null }): string | null {
    if (version.dataVersion == null) return null;
    const description = version.dataDescription?.trim();
    return description ? `data ${version.dataVersion} · ${description}` : `data ${version.dataVersion}`;
}

/**
 * The runs to list. Without "Show archived", those of an archived version are hidden (a run of an unknown version stays).
 * With models chosen, only the runs of a version using at least one of those models stay (an unknown version has no model, so it goes);
 * no model chosen means all of them. The same goes for data versions, and both filters combine (a version must satisfy each one chosen).
 */
export function visibleRuns<T extends { agentVersionId: string }>(
    runs: T[],
    versions: { id: string; archivedAt?: string | null; modelNames: string[]; dataVersion?: string | null }[],
    showArchived: boolean,
    models: readonly string[] = [],
    dataVersions: readonly string[] = [],
): T[] {
    const archived = new Set(versions.filter((version) => version.archivedAt != null).map((version) => version.id));
    const chosen = new Set(models);
    const chosenData = new Set(dataVersions);
    const byId = new Map(versions.map((version) => [version.id, version]));
    return runs.filter((run) => {
        if (!showArchived && archived.has(run.agentVersionId)) return false;
        const version = byId.get(run.agentVersionId);
        if (chosen.size > 0 && version?.modelNames.some((model) => chosen.has(model)) !== true) return false;
        if (chosenData.size > 0 && (version?.dataVersion == null || !chosenData.has(version.dataVersion))) return false;
        return true;
    });
}

/**
 * The ids of the versions whose runs the filters let through, or null when they let through every version (then no
 * version needs to be asked for). An empty list means no version qualifies.
 */
export function allowedVersionIds(
    versions: { id: string; archivedAt?: string | null; modelNames: string[]; dataVersion?: string | null }[],
    showArchived: boolean,
    models: readonly string[],
    dataVersions: readonly string[],
): string[] | null {
    const allowed = visibleRuns(
        versions.map((version) => ({ agentVersionId: version.id })),
        versions,
        showArchived,
        models,
        dataVersions,
    ).map((run) => run.agentVersionId);
    return allowed.length === versions.length ? null : allowed;
}

/**
 * The versions the agent page's problem cards decide over: the ones the filters show, always listed (the server's default for none
 * is every version not archived, which is not what "archived shown" means). Empty when the filters show none.
 */
export function consideredVersionIds(
    versions: { id: string; archivedAt?: string | null; modelNames: string[]; dataVersion?: string | null }[],
    showArchived: boolean,
    models: readonly string[],
    dataVersions: readonly string[],
): string[] {
    return allowedVersionIds(versions, showArchived, models, dataVersions) ?? versions.map((version) => version.id);
}

/** The first `pages` pages of `items` (a page is `PAGE_SIZE` of them) — what a list with "Show more" displays. */
export function firstPages<T>(items: T[], pages: number): T[] {
    return items.slice(0, pages * PAGE_SIZE);
}

/** Whether `items` has more than `pages` pages, so "Show more" has something to show. */
export function hasMorePages(items: unknown[], pages: number): boolean {
    return items.length > pages * PAGE_SIZE;
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
    versions: { id: string; archivedAt?: string | null }[],
    selected: ReadonlySet<string>,
): { toArchive: string[]; toRestore: string[] } {
    const chosen = versions.filter((version) => selected.has(version.id));
    return {
        toArchive: chosen.filter((version) => version.archivedAt == null).map((version) => version.id),
        toRestore: chosen.filter((version) => version.archivedAt != null).map((version) => version.id),
    };
}

/** The page showing what changed between two versions of the agent. */
export const versionCompareHref = (name: string, a: string, b: string) => `${agentHref(name)}/versions/compare?a=${a}&b=${b}`;

/** The two selected versions to compare, or null unless exactly two are selected. */
export function versionsToCompare(versions: { id: string }[], selected: ReadonlySet<string>): [string, string] | null {
    const chosen = versions.filter((version) => selected.has(version.id));
    const [first, second] = chosen;
    return first && second && chosen.length === 2 ? [first.id, second.id] : null;
}

/** The tooltip of "Compare versions": what it does, or why it cannot yet. */
export function compareVersionsTitle(ready: boolean): string {
    return ready ? "Show what changed between the two selected versions" : "Select exactly two versions to compare them";
}

/** A JSON value (a schema, model parameters) as indented text for a code block. */
export function jsonText(value: unknown): string {
    return JSON.stringify(value, null, 2);
}

/** What an agent version is, said on hover where versions are listed (the runs table, the Versions section). */
export const VERSION_HELP =
    "The agent version the run used: its contract as the agent served it when the run started (LLM agents, prompts, models, tools, events, data version). Any change there is a new version; open it to see the contract, or tick two in Versions to compare them.";

/** What the Configuration section's heading says on hover. */
export const CONFIGURATION_HELP =
    "Settings from agent.yaml in the agent's repository. They change with suwageval push and are not part of a version (a version is the contract the running agent serves).";

/** What the samples row says on hover. */
export const SAMPLES_HELP =
    "How many times each scenario's conversation is sent to the agent by default. A scenario or a single check can set its own. A check passes when its share of passing samples reaches its threshold (100% by default).";

/** What the regression branches row says on hover. */
export const REGRESSION_BRANCHES_HELP =
    "Whose runs are reference runs: runs from these branches, with a clean tree and a pushed commit. A check's history is built from them.";

/** What the regression history row says on hover. */
export const REGRESSION_HISTORY_HELP =
    "How many of the newest reference runs make a check's history. A steady pass, then a failure: a Regression. That many failures in a row: a Known failure.";
