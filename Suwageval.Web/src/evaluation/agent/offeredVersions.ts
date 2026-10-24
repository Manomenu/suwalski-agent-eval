// Which versions the runs table's filters offer their options from.
import type { GroupFilter } from "./groupFilter";

/** Which versions go with the runs table's group filter: those with runs in no group (or none yet), those with runs in a group, or all. */
export type VersionGrouping = "none" | "any" | "all";

/** The counts a version's grouping is read from. */
interface Counted {
    runs: number;
    groupRuns: number;
}

function inGrouping(version: Counted, grouping: VersionGrouping): boolean {
    if (grouping === "none") return version.runs > 0 || version.groupRuns === 0;
    if (grouping === "any") return version.groupRuns > 0;
    return true;
}

/**
 * The versions the model, data version and version filters offer their options from: those in the grouping the runs table shows, plus any
 * the filters already have chosen (by id, model or data version), so a choice never disappears from under the person when the table's
 * group filter changes.
 */
export function offeredVersions<T extends Counted & { id: string; modelNames: string[]; data?: readonly { version: string }[] }>(
    versions: T[],
    grouping: VersionGrouping,
    models: readonly string[],
    dataVersions: readonly string[],
    versionIds: readonly string[],
): T[] {
    return versions.filter(
        (version) =>
            inGrouping(version, grouping) ||
            versionIds.includes(version.id) ||
            version.modelNames.some((model) => models.includes(model)) ||
            (version.data ?? []).some((data) => dataVersions.includes(data.version)),
    );
}

/** The grouping of versions that goes with the runs table's group filter (one group by name: all, since a version may have runs in it besides others). */
export function versionGroupingFor(filter: GroupFilter): VersionGrouping {
    return filter.kind === "none" || filter.kind === "any" ? filter.kind : "all";
}
