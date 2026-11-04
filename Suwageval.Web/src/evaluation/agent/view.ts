import { shortDataVersion } from "../../dataVersion/shortDataVersion";
import { COMPARE_MAX_RUNS, COMPARE_MIN_RUNS } from "../../routes/links";
import { versionTitle } from "../../format/names";

export function modelsOf(versions: { modelNames: string[] }[]): string[] {
    return [...new Set(versions.flatMap((version) => version.modelNames))].sort((a, b) => a.localeCompare(b));
}

const DATA_OPTION_DESCRIPTION_MAX = 40;

interface VersionData {
    version: string;
    description?: string | null;
}

export function dataVersionOptions(versions: { data?: readonly VersionData[] }[]): { value: string; label: string }[] {
    const described = new Map<string, string>();
    for (const data of versions.flatMap((version) => version.data ?? [])) {
        const description = data.description?.trim() ?? "";
        if (!described.has(data.version) || (described.get(data.version) === "" && description !== ""))
            described.set(data.version, description);
    }
    return [...described.entries()]
        .sort(([a], [b]) => a.localeCompare(b))
        .map(([value, description]) => ({
            value,
            label:
                description === ""
                    ? shortDataVersion(value)
                    : `${shortDataVersion(value)} · ${description.length > DATA_OPTION_DESCRIPTION_MAX ? `${description.slice(0, DATA_OPTION_DESCRIPTION_MAX).trimEnd()}…` : description}`,
        }));
}

export function versionOptions(versions: { id: string; label?: string | null; createdAt: string }[]): { value: string; label: string }[] {
    return [...versions]
        .sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt))
        .map((version) => ({ value: version.id, label: versionTitle(version.id, version.label) }));
}

interface FilteredVersion {
    id: string;
    archivedAt?: string | null;
    modelNames: string[];
    data?: readonly VersionData[];
}

function versionShown(
    version: FilteredVersion | undefined,
    versionId: string,
    showArchived: boolean,
    models: ReadonlySet<string>,
    versionIds: ReadonlySet<string>,
): boolean {
    if (!showArchived && version?.archivedAt != null) return false;
    if (versionIds.size > 0 && !versionIds.has(versionId)) return false;
    if (models.size > 0 && version?.modelNames.some((model) => models.has(model)) !== true) return false;
    return true;
}

export function visibleRuns<T extends { agentVersionId: string; dataVersion?: string | null }>(
    runs: T[],
    versions: FilteredVersion[],
    showArchived: boolean,
    models: readonly string[] = [],
    dataVersions: readonly string[] = [],
    versionIds: readonly string[] = [],
): T[] {
    const chosen = new Set(models);
    const chosenData = new Set(dataVersions);
    const chosenIds = new Set(versionIds);
    const byId = new Map(versions.map((version) => [version.id, version]));
    return runs.filter((run) => {
        if (!versionShown(byId.get(run.agentVersionId), run.agentVersionId, showArchived, chosen, chosenIds)) return false;
        if (chosenData.size > 0 && (run.dataVersion == null || !chosenData.has(run.dataVersion))) return false;
        return true;
    });
}

export function allowedVersionIds(
    versions: FilteredVersion[],
    showArchived: boolean,
    models: readonly string[],
    dataVersions: readonly string[],
    versionIds: readonly string[] = [],
): string[] | null {
    const chosen = new Set(models);
    const chosenData = new Set(dataVersions);
    const chosenIds = new Set(versionIds);
    const allowed = versions
        .filter(
            (version) =>
                versionShown(version, version.id, showArchived, chosen, chosenIds) &&
                (chosenData.size === 0 || (version.data ?? []).some((data) => chosenData.has(data.version))),
        )
        .map((version) => version.id);
    return allowed.length === versions.length ? null : allowed;
}

// Why: always listed because the server's default for none is every version not archived, which is not what archived shown means
export function consideredVersionIds(
    versions: FilteredVersion[],
    showArchived: boolean,
    models: readonly string[],
    dataVersions: readonly string[],
    versionIds: readonly string[] = [],
): string[] {
    return allowedVersionIds(versions, showArchived, models, dataVersions, versionIds) ?? versions.map((version) => version.id);
}

export function canCompare(count: number): boolean {
    return count >= COMPARE_MIN_RUNS && count <= COMPARE_MAX_RUNS;
}

export function runsToCompare(runs: { id: string }[], selected: ReadonlySet<string>): string[] {
    return runs
        .filter((run) => selected.has(run.id))
        .map((run) => run.id)
        .reverse();
}

export const PROBLEMS_SECTION_HELP =
    "What is wrong with the agent beyond one run: checks that pass in some runs of a version and fail in others, and scenarios where the agent gave no answer. The same as `suwageval problems`.";
export const RUNS_SECTION_HELP = "One run of the scenarios against a version of the agent.";
export const RUN_ID_HELP = "The start of the run's id; open it for its results. Tick runs to compare them.";
export const RUN_STATUS_HELP =
    "Queued, Running, Done, Failed, Cancelled or Interrupted (the server stopped during the run: Run again on its page starts it anew). Reference marks a run that makes the history regressions are measured against; Re-check a run that judged an earlier run's stored answers again.";
export const RUN_GROUP_HELP =
    "The group the run was started in (run --group). A group's runs are experiments: no regression states, never a reference, not in the pass-rate trend.";
export const RUN_CREATED_HELP = "When the run was queued.";
export const RUN_FINISHED_HELP = "When the run ended; empty while it is queued or running.";

export const CONFIGURATION_HELP =
    "Settings from agent.yaml in the agent's repository. They change with suwageval push and are not part of a version (a version is the contract the running agent serves).";

export const SAMPLES_HELP =
    "How many times each scenario's conversation is sent to the agent by default. A scenario or a single check can set its own. A check passes when its share of passing samples reaches its threshold (100% by default).";

export const REGRESSION_BRANCHES_HELP =
    "Whose runs are reference runs: runs from these branches, with a clean tree and a pushed commit. A check's history is built from them.";

export const REGRESSION_HISTORY_HELP =
    "How many of the newest reference runs make a check's history. A steady pass, then a failure: a Regression. That many failures in a row: a Known failure.";
