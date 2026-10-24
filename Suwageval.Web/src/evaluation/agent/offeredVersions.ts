// Which versions the runs table's filters offer their options from.
import { inVersionGrouping, type VersionGrouping } from "../../versions/versionGrouping";
import type { GroupFilter } from "./groupFilter";

/**
 * The versions the model, data version and version filters offer their options from: those in the grouping the runs table shows, plus any
 * the filters already have chosen (by id, model or data version), so a choice never disappears from under the person when the table's
 * group filter changes.
 */
export function offeredVersions<T extends { id: string; groupRunsOnly: boolean; modelNames: string[]; dataVersion?: string | null }>(
    versions: T[],
    grouping: VersionGrouping,
    models: readonly string[],
    dataVersions: readonly string[],
    versionIds: readonly string[],
): T[] {
    return versions.filter(
        (version) =>
            inVersionGrouping(version, grouping) ||
            versionIds.includes(version.id) ||
            version.modelNames.some((model) => models.includes(model)) ||
            (version.dataVersion != null && dataVersions.includes(version.dataVersion)),
    );
}

/** The grouping of versions that goes with the runs table's group filter (one group by name: all, since a version may have runs in it besides others). */
export function versionGroupingFor(filter: GroupFilter): VersionGrouping {
    return filter.kind === "none" || filter.kind === "any" ? filter.kind : "all";
}
